namespace PlaylineConsole
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Luaon.Json;
    using Newtonsoft.Json;
    using Yarn.Compiler;

    public static class CompileCommand
    {
        public static void CompileFiles(FileInfo[] inputs, DirectoryInfo outputDirectory, string outputName, string outputStringTableName, string outputMetadataTableName, bool stdout)
        {
            var compiledResults = PlaylineConsole.CompileProgram(inputs);

            foreach (var diagnostic in compiledResults.Diagnostics)
            {
                Log.Diagnostic(diagnostic);
            }

            if (compiledResults.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                Log.Error($"Not compiling files because errors were encountered.");
                System.Environment.Exit(1);
                return;
            }

            if (stdout)
            {
                EmitCompilationResult(compiledResults, System.Console.Out);
                return;
            }

            // ok so basically in here we do a quick check of the number of files we have
            // if we only have one AND output is the default then we use that as our output name instead of Output
            if (inputs.Length == 1 && outputName.Equals("Output"))
            {
                // weird that this doesn't exist in the FileInfo...
                outputName = Path.GetFileNameWithoutExtension(inputs[0].Name);
            }

            if (string.IsNullOrEmpty(outputStringTableName))
            {
                outputStringTableName = $"{outputName}-Lines.csv";
            }

            var programOutputPath = Path.Combine(outputDirectory.FullName, $"{outputName}.yarnc.lua");
            var stringTableOutputPath = Path.Combine(outputDirectory.FullName, outputStringTableName);

            var serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.Converters.Add(new PlaylineCustomConverter());
            using (var outStream = new FileStream(programOutputPath, FileMode.Create))
            using (var sw = new System.IO.StreamWriter(outStream))
            {
                sw.WriteLine("Playline = Playline or {}");
                sw.WriteLine("Playline.Compiled = Playline.Compiled or {}");
                sw.Write($"Playline.Compiled.{outputName} = ");
                using (var jlw = new JsonLuaWriter(sw)
                {
                    AutoCompleteOnClose = true,
                    Formatting = Formatting.Indented,
                })
                {
                    var lines = compiledResults.StringTable.ToDictionary(kv => kv.Key, kv =>
                    {
                        var stringInfo = kv.Value;
                        var tags = new Dictionary<string, string>();
                        foreach (var tag in stringInfo.metadata)
                        {
                            var tagSplits = tag.ToString().Split(':', 2, StringSplitOptions.TrimEntries);
                            tags.Add(tagSplits[0], tagSplits.Length > 1 ? tagSplits[1] : string.Empty);
                        }
                        if (tags.Count == 0)
                        {
                            tags = null;
                        }
                        return new { Text = stringInfo.text, Node = kv.Value.nodeName, Tags = tags, LineNumber = stringInfo.lineNumber };
                    });
                    serializer.Serialize(jlw, new { Program = compiledResults.Program, Lines = lines });
                }
            }

            Log.Info($"Wrote {programOutputPath}");
        }

        public static CompilationJob GetCompilationJob(FileInfo[] inputs, int languageVersion = Yarn.Compiler.Project.CurrentProjectFileVersion)
        {
            var anyFileIsProject = inputs.Any(i => i.Extension == ".yarnproject");

            if (anyFileIsProject)
            {
                if (inputs.Length > 1)
                {
                    Log.Fatal($"When compiling a Yarn Project file, you must specify only a single file path.");
                }

                var project = Project.LoadFromFile(inputs.First().FullName);

                var job = CompilationJob.CreateFromFiles(project.SourceFiles);

                job.LanguageVersion = project.FileVersion;

                if (project.DefinitionsPath != null)
                {
                    var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(project.DefinitionsPath));

                    if (doc.RootElement.TryGetProperty("Functions", out var functionsArray) && functionsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        static Yarn.IType GetType(string typeName) => typeName.ToLowerInvariant() switch
                        {
                            "string" => Yarn.Types.String,
                            "any" => Yarn.Types.Any,
                            "number" => Yarn.Types.Number,
                            "bool" => Yarn.Types.Boolean,

                            var other => throw new System.ArgumentOutOfRangeException("Invalid type " + other),
                        };

                        foreach (var function in functionsArray.EnumerateArray())
                        {
                            var typeBuilder = new FunctionTypeBuilder();

                            typeBuilder = typeBuilder.WithReturnType(GetType(function.GetProperty("ReturnType").GetString()));

                            foreach (var parameter in function.GetProperty("Parameters").EnumerateArray())
                            {
                                typeBuilder = typeBuilder.WithParameter(GetType(parameter.GetProperty("Type").GetString()));
                            }

                            var decl = new DeclarationBuilder()
                                .WithName(function.GetProperty("YarnName").GetString())
                                .WithType(typeBuilder.FunctionType)
                                .WithDescription(function.GetProperty("Documentation").GetString())
                                .Declaration;

                            job.VariableDeclarations = job.VariableDeclarations.Append(decl);
                        }
                    }
                }

                return job;
            }
            else
            {
                var job = CompilationJob.CreateFromFiles(inputs.Select(i => i.FullName));
                job.LanguageVersion = languageVersion;
                return job;
            }
        }

        private static void EmitCompilationResult(CompilationResult compiledResults, TextWriter textWriter)
        {
            var program = compiledResults.Program;

            var compilerOutput = new Yarn.CompilerOutput();
            compilerOutput.Program = program;

            foreach (var entry in compiledResults.StringTable)
            {
                var tableEntry = new Yarn.StringInfo();
                tableEntry.Text = entry.Value.text;

                compilerOutput.Strings.Add(entry.Key, tableEntry);
            }

            foreach (var diagnostic in compiledResults.Diagnostics)
            {
                var diag = new Yarn.Diagnostic();
                diag.Message = diagnostic.Message;
                diag.FileName = diagnostic.FileName;
                diag.Range = new Yarn.Range
                {
                    Start =
                    {
                        Line = diagnostic.Range.Start.Line,
                        Character = diagnostic.Range.Start.Character,
                    },
                    End =
                    {
                        Line = diagnostic.Range.End.Line,
                        Character = diagnostic.Range.End.Character,
                    },
                };
                diag.Severity = (Yarn.Diagnostic.Types.Severity)diagnostic.Severity;
                compilerOutput.Diagnostics.Add(diag);
            }

            var settings = new Google.Protobuf.JsonFormatter.Settings(true);
            var jsonFormatter = new Google.Protobuf.JsonFormatter(settings);

            jsonFormatter.Format(compilerOutput, textWriter);
        }
    }
}
