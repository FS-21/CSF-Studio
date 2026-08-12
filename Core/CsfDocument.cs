using System;
using System.Collections.Generic;

namespace CsfStudio.Core
{
    public enum CsfLanguage
    {
        LanguageNeutral = -1,
        EnglishUS = 0,
        EnglishUK = 1,
        German = 2,
        French = 3,
        Spanish = 4,
        Italian = 5,
        Japanese = 6,
        Jabberwock = 7,
        Korean = 8,
        Chinese = 9,
        Unknown = 99
    }

    public class CsfStringEntry
    {
        public string Value { get; set; } = string.Empty;
        public string ExtraValue { get; set; } = null;

        public bool HasExtra => !string.IsNullOrEmpty(ExtraValue);

        public CsfStringEntry() { }

        public CsfStringEntry(string value, string extraValue = null)
        {
            Value = value ?? string.Empty;
            ExtraValue = extraValue;
        }

        public CsfStringEntry Clone()
        {
            return new CsfStringEntry(Value, ExtraValue);
        }
    }

    public class CsfLabel
    {
        public string Name { get; set; } = string.Empty;
        public List<CsfStringEntry> Strings { get; set; } = new List<CsfStringEntry>();

        public CsfLabel() { }

        public CsfLabel(string name)
        {
            Name = name ?? string.Empty;
        }

        public CsfLabel(string name, string firstValue, string extraValue = null)
        {
            Name = name ?? string.Empty;
            Strings.Add(new CsfStringEntry(firstValue, extraValue));
        }

        public string FirstValue => Strings.Count > 0 ? Strings[0].Value : string.Empty;
        public string FirstExtraValue => Strings.Count > 0 ? Strings[0].ExtraValue : null;

        public CsfLabel Clone()
        {
            var label = new CsfLabel(Name);
            foreach (var str in Strings)
            {
                label.Strings.Add(str.Clone());
            }
            return label;
        }
    }

    public class CsfDocument
    {
        public int Version { get; set; } = 3;
        public CsfLanguage Language { get; set; } = CsfLanguage.EnglishUS;
        public List<CsfLabel> Labels { get; set; } = new List<CsfLabel>();

        public CsfDocument() { }

        public int TotalStringCount
        {
            get
            {
                int count = 0;
                foreach (var lbl in Labels)
                {
                    count += lbl.Strings.Count;
                }
                return count;
            }
        }

        public int TotalExtraStringCount
        {
            get
            {
                int count = 0;
                foreach (var lbl in Labels)
                {
                    foreach (var s in lbl.Strings)
                    {
                        if (s.HasExtra) count++;
                    }
                }
                return count;
            }
        }

        public CsfDocument Clone()
        {
            var doc = new CsfDocument
            {
                Version = Version,
                Language = Language
            };
            foreach (var lbl in Labels)
            {
                doc.Labels.Add(lbl.Clone());
            }
            return doc;
        }
    }
}
