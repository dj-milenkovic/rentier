using Rentier.Application.Common;
using Rentier.Application.Parsing;

namespace Rentier.Application.Interfaces;

public interface IStatementParser
{
    Task<Result<StatementParseResult, Error>> ParseAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default);
}
