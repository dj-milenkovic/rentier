using System.Reflection;
using System.Reflection.Emit;

namespace Rentier.UnitTests.Architecture;

/// <summary>
/// Reflection-only analysis of which types and members a type uses: its signatures
/// (base type, interfaces, fields, parameters, return types, attributes) and its method
/// bodies (locals plus every type/method/field token in the IL). Nested types are included,
/// which covers compiler-generated closures, iterators, and async state machines.
/// </summary>
internal static class TypeUsageScanner
{
    private const BindingFlags DECLARED_MEMBERS =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
        BindingFlags.Static | BindingFlags.DeclaredOnly;

    private const byte TWO_BYTE_OPCODE_PREFIX = 0xFE;

    private static readonly OpCode[] SINGLE_BYTE_OPCODES = new OpCode[256];
    private static readonly OpCode[] TWO_BYTE_OPCODES = new OpCode[256];

    static TypeUsageScanner()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
                continue;

            var value = unchecked((ushort)opCode.Value);
            if (opCode.Size == 1)
                SINGLE_BYTE_OPCODES[value] = opCode;
            else
                TWO_BYTE_OPCODES[value & 0xFF] = opCode;
        }
    }

    public static TypeUsage Scan(Type type)
    {
        var usage = new TypeUsage();
        ScanType(type, usage);
        return usage;
    }

    private static void ScanType(Type type, TypeUsage usage)
    {
        usage.AddType(type.BaseType);
        foreach (var implemented in type.GetInterfaces())
            usage.AddType(implemented);
        AddAttributes(type.GetCustomAttributesData(), usage);

        foreach (var field in type.GetFields(DECLARED_MEMBERS))
        {
            usage.AddType(field.FieldType);
            AddAttributes(field.GetCustomAttributesData(), usage);
        }

        foreach (var property in type.GetProperties(DECLARED_MEMBERS))
            AddAttributes(property.GetCustomAttributesData(), usage);

        foreach (var method in type.GetMethods(DECLARED_MEMBERS))
        {
            usage.AddType(method.ReturnType);
            ScanMethod(method, type, usage);
        }

        foreach (var constructor in type.GetConstructors(DECLARED_MEMBERS))
            ScanMethod(constructor, type, usage);

        foreach (var nested in type.GetNestedTypes(DECLARED_MEMBERS))
            ScanType(nested, usage);
    }

    private static void ScanMethod(MethodBase method, Type declaringType, TypeUsage usage)
    {
        AddAttributes(method.GetCustomAttributesData(), usage);
        foreach (var parameter in method.GetParameters())
        {
            usage.AddType(parameter.ParameterType);
            AddAttributes(parameter.GetCustomAttributesData(), usage);
        }

        var body = method.GetMethodBody();
        if (body is null)
            return;

        foreach (var local in body.LocalVariables)
            usage.AddType(local.LocalType);
        foreach (var clause in body.ExceptionHandlingClauses)
        {
            if (clause.Flags == ExceptionHandlingClauseOptions.Clause)
                usage.AddType(clause.CatchType);
        }

        var typeArguments = declaringType.IsGenericType ? declaringType.GetGenericArguments() : null;
        var methodArguments = method is MethodInfo { IsGenericMethod: true }
            ? method.GetGenericArguments()
            : null;

        ScanIl(body.GetILAsByteArray() ?? [], method.Module, typeArguments, methodArguments, usage);
    }

    private static void ScanIl(
        byte[] il, Module module, Type[]? typeArguments, Type[]? methodArguments, TypeUsage usage)
    {
        var position = 0;
        while (position < il.Length)
        {
            var opCode = il[position] == TWO_BYTE_OPCODE_PREFIX
                ? TWO_BYTE_OPCODES[il[position + 1]]
                : SINGLE_BYTE_OPCODES[il[position]];
            position += opCode.Size;

            switch (opCode.OperandType)
            {
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, position);
                    usage.AddMember(module.ResolveMember(token, typeArguments, methodArguments));
                    position += 4;
                    break;
                case OperandType.InlineSwitch:
                    position += 4 + (4 * BitConverter.ToInt32(il, position));
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    position += 1;
                    break;
                case OperandType.InlineVar:
                    position += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    position += 8;
                    break;
                default:
                    // InlineBrTarget, InlineI, InlineSig, InlineString, ShortInlineR
                    position += 4;
                    break;
            }
        }
    }

    private static void AddAttributes(IEnumerable<CustomAttributeData> attributes, TypeUsage usage)
    {
        foreach (var attribute in attributes)
        {
            usage.AddType(attribute.AttributeType);
            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Value is Type typeArgument)
                    usage.AddType(typeArgument);
            }
        }
    }
}

/// <summary>The types and members one type (including its nested types) depends on.</summary>
internal sealed class TypeUsage
{
    private readonly HashSet<Type> _types = [];
    private readonly HashSet<MemberInfo> _members = [];

    public IReadOnlySet<Type> Types => _types;

    /// <summary>Methods, constructors, and fields referenced from method bodies.</summary>
    public IReadOnlySet<MemberInfo> Members => _members;

    public void AddMember(MemberInfo? member)
    {
        switch (member)
        {
            case Type type:
                AddType(type);
                break;
            case MethodBase method:
                _members.Add(method);
                AddType(method.DeclaringType);
                if (method is MethodInfo methodInfo)
                {
                    AddType(methodInfo.ReturnType);
                    if (methodInfo.IsGenericMethod)
                    {
                        foreach (var argument in methodInfo.GetGenericArguments())
                            AddType(argument);
                    }
                }

                foreach (var parameter in method.GetParameters())
                    AddType(parameter.ParameterType);
                break;
            case FieldInfo field:
                _members.Add(field);
                AddType(field.DeclaringType);
                AddType(field.FieldType);
                break;
        }
    }

    public void AddType(Type? type)
    {
        if (type is null)
            return;

        if (type.HasElementType)
        {
            AddType(type.GetElementType());
            return;
        }

        if (type.IsGenericParameter || !_types.Add(type))
            return;

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                AddType(argument);
        }
    }
}
