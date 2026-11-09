using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Contracts;

/// <summary>
/// Giữ hợp đồng API: so OpenAPI (Swagger v1) hiện tại với file snapshot đã review, <b>mỗi module (tag Swagger) một file</b>
/// trong <c>Contracts/snapshots/&lt;Tag&gt;.json</c> – hai module song song không đụng cùng file.
/// Đổi API có chủ đích: chạy <c>UPDATE_API_CONTRACTS=1 dotnet test --filter ApiContractTests</c>, commit file snapshot
/// cùng PR để người review thấy thay đổi hợp đồng.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ApiContractTests(PostgresFixture fixture)
{
    private const string UpdateVariable = "UPDATE_API_CONTRACTS";
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public async Task Swagger_matches_reviewed_snapshots_per_module()
    {
        var actual = CurrentContracts();
        var directory = SnapshotDirectory();
        var update = Environment.GetEnvironmentVariable(UpdateVariable) == "1";

        if (update)
        {
            Directory.CreateDirectory(directory);
            foreach (var stale in Directory.GetFiles(directory, "*.json").Where(file => !actual.ContainsKey(Path.GetFileNameWithoutExtension(file))))
            {
                File.Delete(stale);
            }

            foreach (var (tag, json) in actual)
            {
                await File.WriteAllTextAsync(Path.Combine(directory, tag + ".json"), json + "\n");
            }

            return;
        }

        var expectedTags = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet()
            : [];
        var problems = new List<string>();
        foreach (var tag in actual.Keys.Except(expectedTags))
        {
            problems.Add($"Module mới chưa có snapshot: {tag}");
        }

        foreach (var tag in expectedTags.Except(actual.Keys))
        {
            problems.Add($"Snapshot không còn API tương ứng: {tag}");
        }

        foreach (var tag in actual.Keys.Intersect(expectedTags))
        {
            var expected = (await File.ReadAllTextAsync(Path.Combine(directory, tag + ".json"))).TrimEnd();
            if (expected != actual[tag])
            {
                problems.Add($"Hợp đồng API của {tag} đã thay đổi");
            }
        }

        Assert.True(problems.Count == 0,
            string.Join("\n", problems) + $"\nNếu thay đổi là có chủ đích: {UpdateVariable}=1 dotnet test --filter ApiContractTests rồi commit Contracts/snapshots.");
    }

    private Dictionary<string, string> CurrentContracts()
    {
        var provider = fixture.Factory.Services.GetRequiredService<ISwaggerProvider>();
        var document = provider.GetSwagger("v1");
        using var writer = new StringWriter();
        document.SerializeAsV3(new OpenApiJsonWriter(writer));
        var root = JsonNode.Parse(writer.ToString())!.AsObject();

        var schemas = root["components"]?["schemas"]?.AsObject() ?? [];
        var byTag = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var (path, item) in root["paths"]!.AsObject())
        {
            if (path.Contains("/test-probe", StringComparison.Ordinal) || item is null)
            {
                continue;
            }

            var tag = item.AsObject()
                .Select(operation => operation.Value?["tags"]?[0]?.GetValue<string>())
                .FirstOrDefault(name => name is not null) ?? "Untagged";
            if (!byTag.TryGetValue(tag, out var paths))
            {
                paths = [];
                byTag[tag] = paths;
            }

            paths[path] = item.DeepClone();
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (tag, paths) in byTag)
        {
            var used = new SortedDictionary<string, JsonNode?>(StringComparer.Ordinal);
            CollectSchemas(paths, schemas, used);
            var snapshot = new JsonObject
            {
                ["paths"] = Sort(paths),
                ["schemas"] = new JsonObject(used.Select(entry => KeyValuePair.Create(entry.Key, entry.Value?.DeepClone()))),
            };
            result[tag] = snapshot.ToJsonString(Indented);
        }

        return result;
    }

    private static void CollectSchemas(JsonNode? node, JsonObject schemas, SortedDictionary<string, JsonNode?> used)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "$ref" && value?.GetValue<string>() is { } reference && reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                    {
                        var name = reference["#/components/schemas/".Length..];
                        if (!used.ContainsKey(name) && schemas.TryGetPropertyValue(name, out var schema))
                        {
                            used[name] = schema;
                            CollectSchemas(schema, schemas, used);
                        }
                    }
                    else
                    {
                        CollectSchemas(value, schemas, used);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    CollectSchemas(item, schemas, used);
                }

                break;
        }
    }

    private static JsonObject Sort(JsonObject source) =>
        new(source.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => KeyValuePair.Create(entry.Key, entry.Value?.DeepClone())));

    private static string SnapshotDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ClassFund.IntegrationTests.csproj")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục project test."), "Contracts", "snapshots");
    }
}
