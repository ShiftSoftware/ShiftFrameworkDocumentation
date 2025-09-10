using Docs.Shared.Customers;
using Microsoft.AspNetCore.Components;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Docs.Web.Services;

public record ParamDoc(
    bool IsEvent,
    bool Required,
    bool Cascading,
    bool IsChildContent,
    
    string Name,
    string TypeName,
    string? Description,
    string? DefaultValue
);

public class ComponentDocService
{
    public Task<IReadOnlyList<ParamDoc>> DescribeAsync(Type componentType, Func<MemberInfo, string?>? docProvider = null)
    {
        var props = componentType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);

        var list = new List<ParamDoc>();

        foreach (var p in props)
        {
            var isParam = p.GetCustomAttribute<ParameterAttribute>() is not null;
            var isCasc = p.GetCustomAttribute<CascadingParameterAttribute>() is not null;
            if (!isParam && !isCasc) continue;

            var required = p.GetCustomAttribute<EditorRequiredAttribute>() is not null;
            var isEvent = IsEventCallback(p.PropertyType) || typeof(Delegate).IsAssignableFrom(p.PropertyType);
            var isChild = IsChildContent(p.PropertyType);

            string? defaultValue = "";

            try
            {

                Type typeToCreate = componentType.IsGenericTypeDefinition
                    ? componentType.MakeGenericType(typeof(CustomerListDTO))
                    : componentType;

                var instance = Activator.CreateInstance(typeToCreate);

                var getValueMethod = typeToCreate.GetMethod("GetParamValue");
                if (getValueMethod != null)
                {
                    var val = getValueMethod.Invoke(instance, new object[] { p.Name });
                    defaultValue = val is null ? null : ToLiteral(val);
                }
            }
            catch { }

            var desc = p.GetCustomAttribute<DisplayAttribute>()?.Description
                    ?? p.GetCustomAttribute<DescriptionAttribute>()?.Description
                    ?? docProvider?.Invoke(p);

            list.Add(new ParamDoc(
                Name: p.Name,
                TypeName: PrettyTypeName(p.PropertyType),
                Required: required,
                Cascading: isCasc,
                IsEvent: isEvent,
                IsChildContent: isChild,
                DefaultValue: defaultValue,
                Description: desc
            ));
        }

        return Task.FromResult<IReadOnlyList<ParamDoc>>(list.OrderBy(x => x.Name).ToList());
    }

    static bool IsChildContent(Type t) =>
        t == typeof(RenderFragment) ||
        (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(RenderFragment<>));

    static bool IsEventCallback(Type t) =>
        t == typeof(EventCallback) ||
        (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(EventCallback<>));

    static string PrettyTypeName(Type t)
    {
        var nullable = Nullable.GetUnderlyingType(t);
        if (nullable is not null) return $"{PrettyTypeName(nullable)}?";

        if (t.IsGenericType)
        {
            var name = t.Name[..t.Name.IndexOf('`')];
            var args = string.Join(", ", t.GetGenericArguments().Select(PrettyTypeName));
            return $"{name}<{args}>";
        }
        return t.Name;
    }

    static string ToLiteral(object value) =>
        value switch
        {
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? ""
        };
}
