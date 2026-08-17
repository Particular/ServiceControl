namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Text.Json;

// DispatchContext's concrete type is only known at runtime, so round-tripping it through a single JSON column needs the type name stored alongside the payload.
// This has to stay reflection-based. A source-generated JsonSerializerContext isn't possible here because the type isn't known at compile time.
static class DispatchContextSerializer
{
    public static (string TypeName, string Json) Serialize(object dispatchContext)
    {
        var type = dispatchContext.GetType();

        var typeName = $"{type.FullName}, {type.Assembly.GetName().Name}";
        var json = JsonSerializer.Serialize(dispatchContext, type);

        return (typeName, json);
    }

    public static object Deserialize(string typeName, string json)
    {
        var type = Type.GetType(typeName, throwOnError: true)!;

        return JsonSerializer.Deserialize(json, type)!;
    }
}
