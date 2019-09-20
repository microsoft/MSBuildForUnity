using Microsoft.Build.Utilities;

namespace MSBuildForUnity.Tasks
{
    public sealed class AttachDebugger : Task
    {
        /// <summary>
        /// This is a small helper class that provides a task for attaching a debugger to the MSBuild process for the purpose of debugging custom tasks
        /// </summary>
        public override bool Execute()
        {
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Launch();
            }

            return true;
        }
    }
}
