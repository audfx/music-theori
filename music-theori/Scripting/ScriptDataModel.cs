using MoonSharp;
using MoonSharp.Interpreter;

namespace theori.Scripting
{
    /// <summary>
    /// known as `theori` in user scripts.
    /// </summary>
    public sealed class ScriptDataModel : BaseScriptInstance
    {
        public static readonly ScriptDataModel Instance = new ScriptDataModel();

        private ScriptDataModel()
        {
        }

        #region Actual Script Interface Stuff

        public ScriptResources Resources => ScriptResources.StaticResources;

        public ScriptChartDatabaseService ChartDatabase => ScriptChartDatabaseService.Instance;

        #endregion
    }
}
