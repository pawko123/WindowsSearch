using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Hub.Services.Settings;

public sealed class FlexibleEnumYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type.IsEnum;

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        var value = scalar.Value;

        if (Enum.TryParse(type, value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        var normalized = value.Replace("_", string.Empty);
        if (Enum.TryParse(type, normalized, ignoreCase: true, out var normalizedParsed))
        {
            return normalizedParsed;
        }

        return Enum.Parse(type, value, ignoreCase: true);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value != null)
        {
            emitter.Emit(new Scalar(value.ToString()!));
        }
    }
}
