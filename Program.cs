// See https://aka.ms/new-console-template for more information
using MoreLinq;
using ShotcutRandomizer;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Threading.Tasks;


string? path = null;

Begin:
string? input = null;
bool result = false;

while (path == null)
{
    Console.WriteLine("paste in the path to rhe project or folder!");
    path = Console.ReadLine();
}
Console.WriteLine("Reading...");
path = path.Replace("?", string.Empty).Replace(" ", string.Empty);

if (!File.Exists(path))
{
    string[] files = Directory.GetFiles(path, "*.mlt");
    if (files != null)
    {
        Console.WriteLine("projects:");
        for (int i = 0; i < files.Length; i++)
        {
            string item = files[i];
            Console.WriteLine(@$"{i} | {item}");
        }

        Console.WriteLine(@$"Select a project");
        input = Console.ReadLine();
        result = int.TryParse(input, out int p);
        while (!result)
        {
            result = int.TryParse(input, out p);
        }
        path = files[p];
        Console.WriteLine(@$"{path} selected");
    }
    else
    {
        Console.WriteLine("wrong input");
        goto Begin;
    }
}
//read from xml
Mlt? project = null;
XmlSerializer serializer = new(typeof(Mlt));
using (StreamReader reader = new(path))
{
    project = serializer.Deserialize(reader) as Mlt;
    reader.Close();
}

Start:
if (project == null)
{

    Console.WriteLine(@$"project is empty");
    goto Begin;
}

Console.WriteLine(@$"s for shuffle, g for generate, n for new project");
input = Console.ReadLine() ?? "";
while (!input.StartsWith('s') && !input.StartsWith('g') && !input.StartsWith('n'))
{
    goto Start;
}

if (input.StartsWith('n'))
{
    goto Begin;
}

if (input.StartsWith('s'))
{
    //shuffle
    Console.WriteLine("playlist items:");
    for (int i = project.Playlist.Count - 1; i > 0; i--)
    {
        Playlist? item = project.Playlist[i];
        Console.WriteLine(@$"{i} | {item.Id} | {item.Name}");
    }

    Console.WriteLine(@$"Select a playlist id for shuffle");
    input = Console.ReadLine();
    result = int.TryParse(input, out int p);
    while (!result)
    {
        result = int.TryParse(input, out p);
    }

    Console.WriteLine(@$"Shuffling {project.Playlist[p].Name}");
    project.Playlist[p].Blank = new();
    project.Playlist[p].Entry = project.Playlist[p].Entry.Shuffle().ToList();
    goto Write;
}

if (input.StartsWith('g'))
{
    //generate random video, paramters: selected tracks, total duration of video
    Console.WriteLine(@$"Enter duration weight, rec=4, 0 = minimal bias, 0.5 = default, 1.5 = heavy favor shorter videos, 10 = no lang videos");
    input = Console.ReadLine();
    result = double.TryParse(input, out double w);
    if (!result)
    {
        goto Start;
    }

    Console.WriteLine(@$"Enter number weight, rec=0.8, 0 = minimal bias, 0.5 = default, 1.5 = heavy favor shorter videos, 10 = no lang videos");
    input = Console.ReadLine();
    result = double.TryParse(input, out double n);
    if (!result)
    {
        goto Start;
    }

    Console.WriteLine("playlist items:");
    for (int i = project.Playlist.Count - 1; i > 0; i--)
    {
        Playlist? item = project.Playlist[i];
        Console.WriteLine(@$"{i} | {item.Id} | {item.Name}");
    }

    Console.WriteLine(@$"Select playlist ids for shuffle");
    input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        goto Start;
    }

    var vids = new List<Entry>();
    var iarr = input.Split(" ");
    int totalduration = 0;
    foreach (var item in iarr)
    {
        result = int.TryParse(item, out int p);
        if (!result)
        {
            goto Start;
        }

        if (p > project.Playlist.Count)
        {
            goto Start;
        }

        int totaltimeinseconds = project.Playlist[p].Entry.Sum(x => x.Duration);
        Console.WriteLine(@$"{project.Playlist[p].Name} target hours of {TimeSpan.FromSeconds(totaltimeinseconds)}, use 0 to select the entire thing");
        input = Console.ReadLine();
        result = int.TryParse(input, out int h);
        if (!result)
        {
            goto Start;
        }

        if (h == 0)
        {
            h = totaltimeinseconds;
        }
        else
        {
            h = 3600 * h;

        }
        totalduration += h;

        vids.AddRange(new SimulatedAnnealingVideoSelector(h, w, n).SelectVideos(project.Playlist[p].Entry.ToList().Shuffle().ToList()).Shuffle().ToList());
    }

    vids = new SimulatedAnnealingVideoSelector(totalduration, w, n).SelectVideos(vids);

    var newpl = new Playlist
    {
        Entry = vids.Shuffle().ToList(),
        Id = @$"playlist{project.Playlist.Count + 1}",
        Property =
        [
            new()
            {
                Name = "shotcut:video",
                Text = "1"
            },
            new()
            {
                Name = "shotcut:name",
                Text = "generated"
            },
        ]
    };

    project.Playlist.Add(newpl);
    project.Tractor.First(x => x.Property.Any(y => y.Name == "shotcut")).Track.Add(new Track { Producer = newpl.Id });
    goto Write;
}


Write:
//write
Console.WriteLine(@$"writing...");
XmlSerializer writer = new XmlSerializer(typeof(Mlt));
string newpath = Path.Combine(Path.GetDirectoryName(path), @$"{Path.GetFileNameWithoutExtension(path)}.Random{Guid.NewGuid().ToString()[..4]}{Path.GetExtension(path)}");
FileStream file = File.Create(newpath);
writer.Serialize(file, project);
file.Close();
Console.WriteLine(@$"Done writing {newpath}");
goto Begin;

//new Process
//{
//    StartInfo = new ProcessStartInfo(newpath)
//    {
//        UseShellExecute = true
//    }
//}.Start();
