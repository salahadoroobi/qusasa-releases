using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;

namespace Qusasa;

[DataContract]
internal sealed class ReleaseNote
{
    [DataMember(Name = "title")] public string Title { get; private set; } = "";
    [DataMember(Name = "detail")] public string Detail { get; private set; } = "";

    [DataContract]
    private sealed class NotesFile
    {
        [DataMember(Name = "version")] public string Version { get; set; } = "";
        [DataMember(Name = "ar")] public List<ReleaseNote>? Arabic { get; set; }
        [DataMember(Name = "en")] public List<ReleaseNote>? English { get; set; }
        [DataMember(Name = "ja")] public List<ReleaseNote>? Japanese { get; set; }
        [DataMember(Name = "zh")] public List<ReleaseNote>? Chinese { get; set; }
        [DataMember(Name = "fr")] public List<ReleaseNote>? French { get; set; }
        [DataMember(Name = "es")] public List<ReleaseNote>? Spanish { get; set; }
    }

    internal static List<ReleaseNote> Read()
    {
        string assembly = typeof(App).Assembly.GetName().Name;
        var resource = Application.GetResourceStream(new Uri("/" + assembly + ";component/release-notes.json", UriKind.Relative));
        if (resource == null) return new List<ReleaseNote>();
        using var stream = resource.Stream;
        var notes = (NotesFile)new DataContractJsonSerializer(typeof(NotesFile)).ReadObject(stream);
        if (!Version.TryParse(notes.Version, out var version) || version.Build < 0 ||
            version.ToString(3) != typeof(App).Assembly.GetName().Version.ToString(3)) return new List<ReleaseNote>();
        return (Ui.Language switch
        {
            "ar" => notes.Arabic, "ja" => notes.Japanese, "zh" => notes.Chinese,
            "fr" => notes.French, "es" => notes.Spanish, _ => notes.English
        }) ?? notes.English ?? new List<ReleaseNote>();
    }
}
