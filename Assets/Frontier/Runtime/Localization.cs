using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Frontier.Runtime
{
    // Presentation only. English source keys and parameters never alter simulation IDs,
    // command rejection strings, archived catalogs or replay checkpoint hashes.
    public static class Localization
    {
        public const string PreferenceKey="frontier.language.v1";
        public const string SystemPreference="system";
        [Serializable] public sealed class Entry { public string key,value; public bool template; }
        [Serializable] public sealed class Language
        {
            public string code,nativeName,culture;
            public string[] systemLanguages=Array.Empty<string>();
            public Entry[] entries=Array.Empty<Entry>();
        }
        private sealed class Template
        {
            public Regex matcher;
            public string value;
            public int[] arguments;
            public int weight,argumentCount;
            public string prefix,suffix;
            public string[] literals;
        }
        private static readonly Dictionary<string,string> exact=new Dictionary<string,string>(StringComparer.Ordinal);
        private static readonly Dictionary<string,string> names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string,string> cache=new Dictionary<string,string>(StringComparer.Ordinal);
        private static readonly Dictionary<string,string> formats=new Dictionary<string,string>(StringComparer.Ordinal);
        private static Template[] templates=Array.Empty<Template>();
        private static Language[] languages;
        private static string preference,code;
        private static CultureInfo culture;
        public static event Action Changed;
        public static IReadOnlyList<Language> Languages {get{EnsureLoaded();return languages;}}
        public static string Preference {get{EnsureLoaded();return preference;}}
        public static string Code {get{EnsureLoaded();return code;}}
        public static CultureInfo Culture {get{EnsureLoaded();return culture;}}
        public static string CurrentName {get{EnsureLoaded();return languages.First(l=>l.code==code).nativeName;}}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ReloadPreference()
        {
            EnsureCatalogs();Apply(PlayerPrefs.GetString(PreferenceKey,SystemPreference));
        }
        private static void EnsureLoaded(){if(code==null)ReloadPreference();}
        private static void EnsureCatalogs()
        {
            if(languages!=null)return;
            languages=Resources.LoadAll<TextAsset>("Localization").Select(a=>JsonUtility.FromJson<Language>(a.text)).OrderBy(l=>l.code,StringComparer.Ordinal).ToArray();
            if(!languages.Any(l=>l.code=="en"))throw new InvalidOperationException("Missing English localization catalog.");
            foreach(var language in languages)Validate(language);
            if(languages.Select(l=>l.code).Distinct().Count()!=languages.Length)throw new InvalidOperationException("Duplicate language code.");
        }
        public static string ResolveCode(string requested,SystemLanguage systemLanguage)
        {
            EnsureCatalogs();
            if(requested!=SystemPreference&&languages.Any(l=>l.code==requested))return requested;
            return languages.FirstOrDefault(l=>l.systemLanguages.Contains(systemLanguage.ToString()))?.code??"en";
        }
        public static void SetPreference(string value)
        {
            EnsureLoaded();
            if(value!=SystemPreference&&!languages.Any(l=>l.code==value))throw new ArgumentException("Unsupported language.",nameof(value));
            if(value==SystemPreference)PlayerPrefs.DeleteKey(PreferenceKey);else PlayerPrefs.SetString(PreferenceKey,value);
            PlayerPrefs.Save();Apply(value);
        }
        private static void Apply(string requested)
        {
            preference=requested==SystemPreference||languages.Any(l=>l.code==requested)?requested:SystemPreference;
            code=ResolveCode(preference,Application.systemLanguage);exact.Clear();names.Clear();cache.Clear();formats.Clear();
            var selected=languages.First(l=>l.code==code);culture=CultureInfo.GetCultureInfo(selected.culture);
            var patterns=new List<Template>();
            foreach(var entry in selected.entries)
            {
                formats.Add(entry.key,entry.value);
                if(!entry.template){exact.Add(entry.key,entry.value);names[entry.key]=entry.value;continue;}
                var matches=Regex.Matches(entry.key,@"\{(\d+)\}");
                string pattern=Regex.Escape(entry.key);
                foreach(Match match in matches)pattern=pattern.Replace(Regex.Escape(match.Value),"(.*?)");
                var arguments=matches.Cast<Match>().Select(m=>int.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)).ToArray();
                var parts=Regex.Split(entry.key,@"\{\d+\}");
                // A card must not consume the following card through one of its arguments.
                // Only the explicit line joiner can capture more than one line per argument.
                var options=RegexOptions.CultureInvariant;
                if(entry.key=="{0}\n{1}")options|=RegexOptions.Singleline;
                patterns.Add(new Template{matcher=new Regex(@"\A"+pattern+@"\z",options),value=entry.value,
                    arguments=arguments,argumentCount=arguments.Max()+1,prefix=parts[0],suffix=parts[parts.Length-1],
                    weight=parts.Sum(p=>p.Length),literals=parts.Where(s=>s.Length>0).ToArray()});
            }
            templates=patterns.OrderByDescending(t=>t.weight).ToArray();Changed?.Invoke();
        }
        // Exact gettext-style source keys are the normal API. Registered whole-text
        // templates adapt the existing composed UI strings without substring replacement.
        // This keeps bindings in their source language, so cached/hidden labels also switch.
        public static string Text(string source)
        {
            if(string.IsNullOrEmpty(source))return source??"";
            EnsureLoaded();if(code=="en")return source;
            if(cache.TryGetValue(source,out var value))return value;
            value=Translate(source,0);
            if(cache.Count>=2048)cache.Clear();cache[source]=value;return value;
        }
        private static string Translate(string source,int depth)
        {
            if(exact.TryGetValue(source,out var value))return value;
            if(source.Length==0||source.Length>4096||depth>=12)return source;
            string trimmed=source.Trim();
            if(trimmed.Length>0&&names.TryGetValue(trimmed,out value))
            {
                if(trimmed==trimmed.ToUpperInvariant())value=value.ToUpperInvariant();
                else if(trimmed==trimmed.ToLowerInvariant())value=value.ToLowerInvariant();
                int start=source.IndexOf(trimmed,StringComparison.Ordinal);
                return source.Substring(0,start)+value+source.Substring(start+trimmed.Length);
            }
            foreach(var template in templates)
            {
                if(!source.StartsWith(template.prefix,StringComparison.Ordinal)||!source.EndsWith(template.suffix,StringComparison.Ordinal))continue;
                int offset=0;bool possible=true;
                foreach(string literal in template.literals)
                {int index=source.IndexOf(literal,offset,StringComparison.Ordinal);if(index<0){possible=false;break;}offset=index+literal.Length;}
                if(!possible)continue;
                var match=template.matcher.Match(source);if(!match.Success)continue;
                var args=new object[template.argumentCount];bool smaller=true;
                for(int i=0;i<template.arguments.Length;i++)
                {var part=match.Groups[i+1].Value;if(part.Length>=source.Length){smaller=false;break;}args[template.arguments[i]]=Translate(part,depth+1);}
                if(smaller)return string.Format(Culture,template.value,args);
            }
            return source;
        }
        // New UI code can use an explicit key/template instead of the legacy adapter.
        public static string Format(string key,params object[] arguments)
        {
            EnsureLoaded();formats.TryGetValue(key,out string format);
            return string.Format(Culture,format??key,arguments.Select(a=>a is string s?Text(s):a).ToArray());
        }
        public static void Validate(Language language)
        {
            if(language==null||string.IsNullOrWhiteSpace(language.code)||string.IsNullOrWhiteSpace(language.nativeName))throw new ArgumentException("Invalid localization catalog.");
            CultureInfo.GetCultureInfo(language.culture);
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in language.entries)
            {
                if(string.IsNullOrEmpty(entry.key)||string.IsNullOrEmpty(entry.value)||!keys.Add(entry.key))throw new ArgumentException("Invalid or duplicate translation key: "+entry.key);
                if(!entry.template)continue;
                var source=Regex.Matches(entry.key,@"\{(\d+)\}").Cast<Match>().Select(m=>m.Value).OrderBy(s=>s).ToArray();
                var translated=Regex.Matches(entry.value,@"\{(\d+)\}").Cast<Match>().Select(m=>m.Value).OrderBy(s=>s).ToArray();
                if(source.Length==0||source.Distinct().Count()!=source.Length||!source.SequenceEqual(translated))throw new ArgumentException("Translation parameters differ: "+entry.key);
                string.Format(CultureInfo.InvariantCulture,entry.value,Enumerable.Repeat<object>("test",source.Length).ToArray());
            }
        }
    }
}
