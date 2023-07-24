using System;
using System.Collections.Generic;
using System.Linq;
using WindowsFirewallHelper;

namespace IIGO.Services
{
    internal static class Extensions
    {
        // https://codereview.stackexchange.com/a/219223/274517
        public static List<(int from, int to)> Ranges(List<int> nums)
        {
            nums = nums.OrderBy(a => a).Distinct().ToList();

            var ranges = new List<(int from, int to)>();

            var start = 0;
            while (start < nums.Count)
            {
                var end = start + 1;   // the range is from [start, end).

                while (end < nums.Count && nums[end - 1] + 1 == nums[end])
                {
                    end++;
                }

                ranges.Add((nums[start], nums[end - 1]));
                start = end;   // continue after the current range
            }

            return ranges;
        }

        public static string RangeStr(List<(int from, int to)> ranges)
        {
            var parts = new List<string>();

            foreach (var range in ranges)
            {
                if (range.from == range.to)
                {
                    parts.Add(range.from.ToString());
                }
                else
                {
                    parts.Add(range.@from + "-" + range.to);
                }
            }

            return string.Join(", ", parts);
        }

        public static string AsRangeString(this IFirewallRule rule) => RangeStr(Ranges(rule.LocalPorts.Select(x => Convert.ToInt32(x)).ToList()));
    }
}
