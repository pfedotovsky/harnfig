using YamlDotNet.RepresentationModel;

namespace Harnfig;

internal static class YamlHelpers
{
    public static YamlMappingNode LoadMapping(string yaml, string sourceName)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));

            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode mapping)
            {
                throw new ConfigurationException($"{sourceName} must contain exactly one YAML mapping.");
            }

            return mapping;
        }
        catch (ConfigurationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ConfigurationException($"Could not parse {sourceName}: {exception.Message}", exception);
        }
    }

    public static void RequireOnlyKeys(YamlMappingNode mapping, string context, params string[] allowed)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var keyNode in mapping.Children.Keys)
        {
            var key = Scalar(keyNode, $"{context} key");
            if (!allowedSet.Contains(key))
            {
                throw new ConfigurationException($"Unknown field '{key}' in {context}.");
            }
        }
    }

    public static YamlNode? Optional(YamlMappingNode mapping, string key)
        => mapping.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

    public static YamlNode Required(YamlMappingNode mapping, string key, string context)
        => Optional(mapping, key) ?? throw new ConfigurationException($"Missing required field '{key}' in {context}.");

    public static string RequiredScalar(YamlMappingNode mapping, string key, string context)
    {
        var value = Scalar(Required(mapping, key, context), $"{context}.{key}").Trim();
        if (value.Length == 0)
        {
            throw new ConfigurationException($"Field '{key}' in {context} cannot be empty.");
        }

        return value;
    }

    public static string? OptionalScalar(YamlMappingNode mapping, string key, string context)
    {
        var node = Optional(mapping, key);
        if (node is null)
        {
            return null;
        }

        var value = Scalar(node, $"{context}.{key}").Trim();
        return value.Length == 0 ? null : value;
    }

    public static int RequiredInt(YamlMappingNode mapping, string key, string context)
    {
        var value = RequiredScalar(mapping, key, context);
        return int.TryParse(value, out var result)
            ? result
            : throw new ConfigurationException($"Field '{key}' in {context} must be an integer.");
    }

    public static YamlSequenceNode RequiredSequence(YamlMappingNode mapping, string key, string context)
        => Required(mapping, key, context) as YamlSequenceNode
           ?? throw new ConfigurationException($"Field '{key}' in {context} must be a sequence.");

    public static YamlMappingNode Mapping(YamlNode node, string context)
        => node as YamlMappingNode
           ?? throw new ConfigurationException($"{context} must be a mapping.");

    public static string Scalar(YamlNode node, string context)
        => node is YamlScalarNode scalar && scalar.Value is not null
            ? scalar.Value
            : throw new ConfigurationException($"{context} must be a scalar value.");

    public static string Quote(string value)
        => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal)}\"";
}
