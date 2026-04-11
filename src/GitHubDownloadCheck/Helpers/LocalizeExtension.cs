using System;
using System.Globalization;
using Avalonia.Markup.Xaml;
using GitHubDownloadCheck.Resources;

namespace GitHubDownloadCheck.Helpers;

/// <summary>
/// AXAML markup extension for localized strings.
/// Usage: {h:Localize KeyName}
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public LocalizeExtension() { }
    public LocalizeExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
        => Strings.ResourceManager.GetString(Key, CultureInfo.CurrentUICulture) ?? Key;
}
