using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elsa.Workflow.Infrastructure.Serialization;

/// <summary>
/// Teaches the MongoDB BSON driver how to round-trip <see cref="JsonObject"/> values.
///
/// Without this, the driver falls back to <see cref="DictionaryInterfaceImplementerSerializer{TDictionary,TKey,TValue}"/>
/// (because <see cref="JsonObject"/> implements <c>IDictionary&lt;string, JsonNode?&gt;</c>)
/// and calls <c>Activator.CreateInstance&lt;JsonObject&gt;()</c> as the accumulator.
/// That throws <see cref="MissingMethodException"/> because <c>JsonObject</c> has no
/// truly parameterless constructor at the IL level — its only constructor takes an
/// optional <c>JsonNodeOptions?</c> parameter, which is not the same thing.
///
/// Registration happens in <see cref="ElsaInfrastructureExtensions.UseElsaMongoDb"/>.
/// </summary>
internal sealed class JsonObjectBsonSerializer : SerializerBase<JsonObject?>
{
    public static readonly JsonObjectBsonSerializer Instance = new();

    private static readonly JsonWriterSettings RelaxedJsonSettings = new()
    {
        OutputMode = JsonOutputMode.RelaxedExtendedJson
    };

    public override JsonObject? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return null;
        }

        // Read as a raw BsonDocument, then convert to JsonObject via relaxed JSON.
        // RelaxedExtendedJson produces standard-compatible JSON (no "$numberInt" wrappers)
        // that System.Text.Json can parse without extra converters.
        var document = BsonDocumentSerializer.Instance.Deserialize(context, args);
        var json = document.ToJson(RelaxedJsonSettings);
        return JsonSerializer.Deserialize<JsonObject>(json);
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JsonObject? value)
    {
        if (value is null)
        {
            context.Writer.WriteNull();
            return;
        }

        var json = value.ToJsonString();
        var document = BsonDocument.Parse(json);
        BsonDocumentSerializer.Instance.Serialize(context, args, document);
    }
}
