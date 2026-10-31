using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace WindowsSearch.Common.Models;

public abstract class CommandDetailsBase
{
    public List<CommandDetail> ToDetails()
    {
        var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.GetCustomAttribute<DisplayAttribute>()?.GetOrder() ?? 1000);

        var list = new List<CommandDetail>();
        foreach (var prop in properties)
        {
            var val = prop.GetValue(this)?.ToString();
            if (string.IsNullOrEmpty(val)) continue;

            var display = prop.GetCustomAttribute<DisplayAttribute>();
            var label = display?.Name ?? prop.Name;
            list.Add(new CommandDetail(label, val));
        }

        return list;
    }
}
