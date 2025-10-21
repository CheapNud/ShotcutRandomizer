using System;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace CheapShotcutRandomizer
{
    [XmlRoot(ElementName = "profile")]
    public class Profile
    {
        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }
        [XmlAttribute(AttributeName = "width")]
        public string Width { get; set; }
        [XmlAttribute(AttributeName = "height")]
        public string Height { get; set; }
        [XmlAttribute(AttributeName = "progressive")]
        public string Progressive { get; set; }
        [XmlAttribute(AttributeName = "sample_aspect_num")]
        public string Sample_aspect_num { get; set; }
        [XmlAttribute(AttributeName = "sample_aspect_den")]
        public string Sample_aspect_den { get; set; }
        [XmlAttribute(AttributeName = "display_aspect_num")]
        public string Display_aspect_num { get; set; }
        [XmlAttribute(AttributeName = "display_aspect_den")]
        public string Display_aspect_den { get; set; }
        [XmlAttribute(AttributeName = "frame_rate_num")]
        public string Frame_rate_num { get; set; }
        [XmlAttribute(AttributeName = "frame_rate_den")]
        public string Frame_rate_den { get; set; }
        [XmlAttribute(AttributeName = "colorspace")]
        public string Colorspace { get; set; }
    }

    [XmlRoot(ElementName = "property")]
    public class Property
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "chain")]
    public class Chain
    {
        [XmlElement(ElementName = "property")]
        public List<Property> Property { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "out")]
        public string Out { get; set; }
    }

    [XmlRoot(ElementName = "entry")]
    public class Entry
    {
        [XmlAttribute(AttributeName = "producer")]
        public string Producer { get; set; }
        [XmlAttribute(AttributeName = "in")]
        public string In { get; set; }
        [XmlAttribute(AttributeName = "out")]
        public string Out { get; set; }

        public int Duration => Convert.ToInt32(Math.Floor((TimeSpan.Parse(Out) - TimeSpan.Parse(In)).TotalSeconds)); // Duration in seconds
    }

    [XmlRoot(ElementName = "playlist")]
    public class Playlist
    {
        [XmlElement(ElementName = "property")]
        public List<Property> Property { get; set; }
        [XmlElement(ElementName = "entry")]
        public List<Entry> Entry { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }
        [XmlElement(ElementName = "blank")]
        public List<Blank> Blank { get; set; }

        public string Name => Property.FirstOrDefault(x => x.Name == $@"shotcut:name")?.Text ?? "system track";
    }

    [XmlRoot(ElementName = "producer")]
    public class Producer
    {
        [XmlElement(ElementName = "property")]
        public List<Property> Property { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "in")]
        public string In { get; set; }
        [XmlAttribute(AttributeName = "out")]
        public string Out { get; set; }
    }

    [XmlRoot(ElementName = "blank")]
    public class Blank
    {
        [XmlAttribute(AttributeName = "length")]
        public string Length { get; set; }
    }

    [XmlRoot(ElementName = "track")]
    public class Track
    {
        [XmlAttribute(AttributeName = "producer")]
        public string Producer { get; set; }
        [XmlAttribute(AttributeName = "in")]
        public string In { get; set; }
        [XmlAttribute(AttributeName = "out")]
        public string Out { get; set; }
        [XmlAttribute(AttributeName = "hide")]
        public string Hide { get; set; }
    }

    [XmlRoot(ElementName = "transition")]
    public class Transition
    {
        [XmlElement(ElementName = "property")]
        public List<Property> Property { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "out")]
        public string Out { get; set; }
    }

    [XmlRoot(ElementName = "tractor")]
    public class Tractor
    {
        [XmlElement(ElementName = "property")]
        public List<Property> Property { get; set; }
        [XmlElement(ElementName = "track")]
        public List<Track> Track { get; set; }
        [XmlElement(ElementName = "transition")]
        public List<Transition> Transition { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "in")]
        public string In { get; set; }
        [XmlAttribute(AttributeName = "out")]
        public string Out { get; set; }
        [XmlElement(ElementName = "properties")]
        public Properties Properties { get; set; }
        [XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }
    }

    [XmlRoot(ElementName = "properties")]
    public class Properties
    {
        [XmlElement(ElementName = "property")]
        public List<Property> Property { get; set; }
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }

    [XmlRoot(ElementName = "mlt")]
    public class Mlt
    {
        [XmlElement(ElementName = "profile")]
        public Profile Profile { get; set; }
        [XmlElement(ElementName = "chain")]
        public List<Chain> Chain { get; set; }
        [XmlElement(ElementName = "playlist")]
        public List<Playlist> Playlist { get; set; }
        [XmlElement(ElementName = "producer")]
        public Producer Producer { get; set; }
        [XmlAttribute(AttributeName = "producer")]
        public string _Producer { get; set; }
        [XmlElement(ElementName = "tractor")]
        public List<Tractor> Tractor { get; set; }
        [XmlAttribute(AttributeName = "LC_NUMERIC")]
        public string LC_NUMERIC { get; set; }
        [XmlAttribute(AttributeName = "version")]
        public string Version { get; set; }
        [XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }
    }
}
