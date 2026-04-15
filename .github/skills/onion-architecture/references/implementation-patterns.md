# Implementation Patterns per Layer

Reference code examples for each layer in PAM. Load this file when you need concrete
implementation guidance beyond what the SKILL.md summary provides.

---

## Domain Layer

### Rich entity with state transitions

The domain entity is the right place for business rules and state machine logic.
Keep properties immutable (private setters) and expose behavior through explicit methods.

```csharp
public class Project : BaseModel
{
    private static readonly HashSet<(ProjectStatus From, ProjectStatus To)> AllowedTransitions =
    [
        (ProjectStatus.Active, ProjectStatus.DeleteInProgress),
        (ProjectStatus.DeleteInProgress, ProjectStatus.Deleted),
    ];

    public Project(
        ProjectLite projectLite,
        List<DataLite> data,
        List<DatasetLite> datasets,
        List<ModelLite> models,
        List<CalculationLite> calculations)
        : base(projectLite.Id, projectLite.CreatedBy, projectLite.CreatedAt,
               projectLite.ModifiedBy, projectLite.ModifiedAt)
    {
        Name = projectLite.Name ?? throw new ArgumentException("Name is required");
        Data = data ?? throw new ArgumentNullException(nameof(data));

        if (data.GroupBy(d => d.Name).Any(g => g.Count() > 1))
            throw new ArgumentException("data contains duplicate names.");

        Datasets = datasets ?? throw new ArgumentNullException(nameof(datasets));
        Models = models ?? throw new ArgumentNullException(nameof(models));
        Calculations = calculations ?? throw new ArgumentNullException(nameof(calculations));
    }

    public string Name { get; }
    public ProjectStatus Status { get; private set; }
    private List<DataLite> Data { get; }

    public void UpdateStatus(ProjectStatus newStatus)
    {
        if (!AllowedTransitions.Contains((Status, newStatus)))
            throw new InvalidEntityStateException("Project", newStatus.ToString(), "...");

        Status = newStatus;
    }

    public void AddDataFile(DataFileLite dataFile)
    {
        if (Data.Exists(d => d.Name == dataFile.Name))
            throw new UniqueNameException($"Data with name '{dataFile.Name}' already exists.");

        Data.Add(dataFile);
    }
}
```

### Domain service (logic spanning multiple entities)

When a rule involves multiple entities that don't belong to the same aggregate, use a static
domain service rather than cluttering the entity or leaking logic into the use case.

```csharp
public static class CalculationProcessDomainService
{
    public static IInterpretedPrediction RunInterpret(
        Calculation calculation,
        ProjectStatus projectStatus,
        Guid userId,
        Guid predictionId,
        List<InterpretProcessLite> interprets,
        List<InterpretProcessLite> metrics,
        List<MasterParametersSet> masterParametersSets)
    {
        if (projectStatus != ProjectStatus.Active)
            throw new ProjectNotActiveException(calculation.ProjectId);

        if (!calculation.IsFitCompleted())
            throw new FitMustBeCompletedException(calculation.Id);

        var prediction = calculation.GetPrediction(predictionId);
        var interpretProcess = new InterpretProcess(
            Guid.NewGuid(), userId, DateTime.UtcNow,
            interprets, metrics, masterParametersSets);

        prediction.AddInterpretProcess(interpretProcess);
        return prediction;
    }
}
```

### Domain exceptions

Throw domain exceptions from entity methods. Use specific exception types so callers can
handle them selectively.

```csharp
public class UniqueNameException : Exception
{
    public UniqueNameException(string message) : base(message) { }
}

public class InvalidEntityStateException : Exception
{
    public InvalidEntityStateException(string entityType, string state, string allowedStates)
        : base($"{entityType} cannot transition to '{state}'. {allowedStates}") { }
}
```

---

## Application Layer

### Use case implementation

```csharp
public class CreateOrUpdateProjectUseCase
    : WriteUseCase<Project, CreateProjectDto, ProjectDto, BaseFilterQueryModel, ProjectCreationFailedException>,
      ICreateOrUpdateProjectUseCase
{
    private readonly IProjectsRepository _projectRepository;

    public CreateOrUpdateProjectUseCase(
        IProjectsRepository projectRepository,
        IMapper mapper,
        ILogger<CreateOrUpdateProjectUseCase> logger)
        : base(projectRepository, mapper, logger)
    {
        _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
    }

    protected override async Task ValidateBeforeCreate(CreateProjectDto newData, Project model)
    {
        var allProjects = await _projectRepository.FindLiteAsync(new SortQueryModel());
        var projects = new Domain.Projects.Projects(allProjects);
        projects.ValidateIfProjectNameExists(newData.Name);
    }
}
```

### Exception handling in use cases

Let domain exceptions propagate — they carry business meaning. Catch and wrap only
infrastructure exceptions (e.g. failed AWS job, DB timeout) before rethrowing as
application-level failures. Always log before rethrowing.

```csharp
try
{
    await _sagaOrchestrator.ExecuteAsync(sagaSteps);
}
catch (EntityConflictException)
{
    throw; // Domain exception — propagate as-is
}
catch (JobLaunchFailedException jlex)
{
    _logger.LogError(jlex, "Job launch failed for prediction: {PredictionId}", predictionId);
    throw new RunInterpretProcessFailedException("Interpret process job launch failed.");
}
```

### Port interface (repository)

Ports work with domain entities, not DTOs. Return nullable types when an entity might not exist.

```csharp
public interface IProjectsRepository
    : IBaseRepository<Project, BaseFilterQueryModel>,
      IBaseReadRepository<Project, ProjectLite, BaseFilterQueryModel>
{
    Task<int> UpdateStatusAsync(Project project);
    Task<int> DeleteAndUpdateStatusProjectAsync(Guid projectId);
    Task<bool> HasAnyInProgressWorkAsync(Guid projectId);
    Task<ProjectStatus?> GetProjectStatusAsync(Guid projectId);
}
```

---

## Infrastructure Layer

### Repository implementation

```csharp
public class ProjectsRepository
    : BaseRepository<ProjectDbModel, ProjectLite, Project, ProjectLite, BaseFilterQueryModel, BaseDbFilterModel>,
      IProjectsRepository
{
    private readonly IProjectsDbContext _projectsContext;
    private readonly IDataRepository _dataRepository;

    public ProjectsRepository(
        IProjectsDbContext projectsContext,
        IProjectsSortQuerySupport projectsSortQuerySupport,
        IMapper mapper,
        IDataRepository dataRepository)
        : base(projectsContext, projectsSortQuerySupport, mapper)
    {
        _projectsContext = projectsContext ?? throw new ArgumentNullException(nameof(projectsContext));
        _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
    }

    protected override async Task<Project> MapToDomainModel(ProjectDbModel dbModel)
    {
        var data = await _dataRepository.FindLiteAsync(
            new SortQueryModel(),
            new DataFilterQueryModel { ProjectId = dbModel.Id });

        return dbModel.ToDomainModel(data, ...);
    }
}
```

---

## Presentation Layer

### Controller

```csharp
[Route("api/predictive-analytics-manager/v1/projects")]
[ApiController]
public class ProjectsController
    : CrudController<CreateProjectDto, ProjectDto, CreateProjectDto, BaseFilterQueryModel>
{
    private readonly IValidator<Guid> _guidValidator;
    private readonly IDeleteProjectsUseCase _deleteProjectsUseCase;

    public ProjectsController(
        ICreateOrUpdateProjectUseCase createOrUpdateProjectUseCase,
        IValidator<CreateProjectDto> createValidator,
        IValidator<Guid> guidValidator,
        IGetProjectsUseCase getProjectsUseCase,
        IDeleteProjectsUseCase deleteProjectsUseCase,
        IProjectsSortQuerySupport projectsSortQuerySupport,
        ISystemAuthorizationService systemAuthorizationService)
        : base(createOrUpdateProjectUseCase, createValidator, getProjectsUseCase,
               projectsSortQuerySupport, systemAuthorizationService)
    {
        _guidValidator = guidValidator ?? throw new ArgumentNullException(nameof(guidValidator));
        _deleteProjectsUseCase = deleteProjectsUseCase ?? throw new ArgumentNullException(nameof(deleteProjectsUseCase));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDocumentRoot<IEnumerable<ProjectDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ApiResponseDocumentRoot<IEnumerable<ProjectDto>>>> GetAsync(
        [FromQuery] PageQueryModel pageQuery,
        [FromQuery] SortQueryModel sortQueryModel) =>
        HandleGetAsync(pageQuery, sortQueryModel);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDocumentRoot<ProjectDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ApiResponseDocumentRoot<ProjectDto>>> CreateAsync(
        [FromBody, BindRequired] ApiInputDocumentRoot<CreateProjectDto> value) =>
        HandleCreateAsync(value);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteAsync(Guid id)
    {
        var validationResult = _guidValidator.Validate(id);
        if (!validationResult.IsValid)
            return validationResult.ToErrorObjectResult(this);

        await _deleteProjectsUseCase.DeleteAsync(id, GetUserContext().TenantData);
        return NoContent();
    }
}
```
