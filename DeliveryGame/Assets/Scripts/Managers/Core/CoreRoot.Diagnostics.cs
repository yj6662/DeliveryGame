using System.Text;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed partial class CoreRoot
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogSubManagerInitOrder()
        {
            var builder = new StringBuilder(256);
            builder.Append("[CoreRoot] SubManager Init Order:");
            for (int i = 0; i < _subManagers.Count; i++)
            {
                ISubManager manager = _subManagers[i];
                if (manager == null)
                {
                    continue;
                }

                builder.Append("\n - ");
                builder.Append(manager.InitOrder);
                builder.Append(" | ");
                builder.Append(manager.Name);
                builder.Append(" | ");
                builder.Append(manager.GetType().FullName);
            }

            Debug.Log(builder.ToString());
        }

        private void LogServiceSnapshot()
        {
            const int maxServiceCount = 64;
            var names = new string[maxServiceCount];
            int count = _services.CopyRegisteredServiceTypeNamesNonAlloc(names);

            var builder = new StringBuilder(256);
            builder.Append("[CoreRoot] Registered Services:");
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                {
                    continue;
                }

                builder.Append("\n - ");
                builder.Append(names[i]);
            }

            Debug.Log(builder.ToString());
        }
#endif
    }
}
