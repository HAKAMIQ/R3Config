using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace R3Config.Lang;

public sealed class TextExt : MarkupExtension
{
    public TextExt()
    {
    }

    public TextExt(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        var binding = new Binding($"[{Key}]")
        {
            Source = UiText.Current,
            Mode = BindingMode.OneWay,
            FallbackValue = Key,
            TargetNullValue = Key
        };

        return binding.ProvideValue(serviceProvider);
    }
}