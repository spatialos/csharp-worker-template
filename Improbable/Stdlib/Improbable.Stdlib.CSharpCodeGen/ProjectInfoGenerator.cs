
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Improbable.Stdlib.Project;
using Newtonsoft.Json;
using ProjectConfig = Improbable.Stdlib.Project.ProjectConfig;
using static Improbable.CSharpCodeGen.Case;

namespace Improbable.Stdlib.CSharpCodeGen
{
    public class ProjectInfoGenerator
    {
        public string GenerateProjectInfo(string pathToSpatialOsJson, string ns = "SpatialOsProject")
        {
            var project = JsonConvert.DeserializeObject<ProjectConfig>(File.ReadAllText(pathToSpatialOsJson, Encoding.UTF8));
            var configDir = Path.GetDirectoryName(pathToSpatialOsJson) ?? Environment.CurrentDirectory;

            var layers = new HashSet<string>();
            var workerTypes = new HashSet<string>();
            var mapping = new Dictionary<string, List<string>>();

            foreach (var workerPath in project.ClientWorkers.Union(project.ServerWorkers))
            {
                var filePath = Path.IsPathRooted(workerPath) ? workerPath : Path.Combine(configDir, workerPath);
                var workerConfig = JsonConvert.DeserializeObject<WorkerConfig>(File.ReadAllText(filePath, Encoding.UTF8));

                layers.Add(workerConfig.Layer);
                workerTypes.Add(workerConfig.WorkerType);

                if (!mapping.TryGetValue(workerConfig.Layer, out var layer))
                {
                    layer = mapping[workerConfig.Layer] = new List<string>();
                }

                layer.Add(workerConfig.WorkerType);
            }

            return $@"namespace {ns}
{{
    public static class WorkerTypes
    {{
        public static string[] AllWorkerTypes = {{ {string.Join(", ", workerTypes.Select(workerType => $"WorkerTypes.{SnakeCaseToPascalCase(workerType)}"))} }};
        public static string[] AllWorkerLayers = {{ {string.Join(", ", layers.Select(layer => $"Layers.{SnakeCaseToPascalCase(layer)}"))} }};

{Indent(2, string.Join("\n", workerTypes.Select(workerType => $@"public static string {SnakeCaseToPascalCase(workerType)} = ""{workerType}"";")))}
    }}

    public static class Layers
    {{
{Indent(2, string.Join("\n", mapping.Keys.Select(layer => $@"public static string {SnakeCaseToPascalCase(layer)} = ""{layer}"";")))}
    }}
}}";

        }
    }
}
