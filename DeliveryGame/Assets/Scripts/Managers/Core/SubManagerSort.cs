using System;

namespace DeliveryRun.Managers.Core
{
    internal static class SubManagerSort
    {
        public static int Compare(ISubManager left, ISubManager right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int byOrder = left.InitOrder.CompareTo(right.InitOrder);
            if (byOrder != 0)
            {
                return byOrder;
            }

            string leftName = left.Name ?? string.Empty;
            string rightName = right.Name ?? string.Empty;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }
    }
}
