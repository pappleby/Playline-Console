namespace PlaylineConsole
{
    using System;
    using System.IO;

    public static class DumpCompiledCodeCommand
    {
        public static void DumpCompiledCode(FileInfo[] inputs, bool allowPreviewFeatures)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            var compiledResults = PlaylineConsole.CompileProgram(inputs);

            System.Func<string, string> stringLookupHelper = (input) =>
            {
                if (compiledResults.StringTable.TryGetValue(input, out var result))
                {
                    return result.text;
                }
                else
                {
                    return null;
                }
            };

            Console.WriteLine(Yarn.Compiler.Utility.GetCompiledCodeAsString(compiledResults.Program, null, compiledResults));
        }
    }
}
