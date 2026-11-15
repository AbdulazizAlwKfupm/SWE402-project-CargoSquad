using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CargoSquad.Tests
{
    /// <summary>Lets the character art pipeline start test runs and writes the outcome to Temp/CharacterArt/tests.txt.</summary>
    [InitializeOnLoad]
    public static class CharacterArtTestBridge
    {
        private static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CharacterArt"));

        static CharacterArtTestBridge()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        }

        public static string Run(string mode)
        {
            string file = Path.Combine(Folder, "tests.txt");
            if (File.Exists(file))
                File.Delete(file);
            var filter = new Filter { testMode = mode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode };
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
            return $"started {mode}";
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var text = new StringBuilder();
                text.AppendLine($"{result.TestStatus} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} duration={result.Duration:F1}s");
                Append(result, text);
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "tests.txt"), text.ToString());
            }

            private static void Append(ITestResultAdaptor result, StringBuilder text)
            {
                if (!result.HasChildren)
                {
                    text.AppendLine($"{result.TestStatus}: {result.FullName}");
                    if (!string.IsNullOrEmpty(result.Message))
                        text.AppendLine("  " + result.Message.Replace("\n", "\n  "));
                    if (!string.IsNullOrEmpty(result.Output))
                        text.AppendLine("  output: " + result.Output.Replace("\n", "\n  "));
                    return;
                }

                foreach (ITestResultAdaptor child in result.Children)
                    Append(child, text);
            }
        }
    }
}
