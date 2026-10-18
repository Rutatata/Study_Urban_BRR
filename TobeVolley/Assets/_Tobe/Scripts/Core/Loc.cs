// Языки игры: русский (исходный текст в коде), английский, японский. Перевод — по таблице Resources/Loc/strings.txt
// (строка: русский<TAB>английский<TAB>японский; «\n» — перенос строки; {0}, {1} — подставляемые части: «ДО {0}»).
// Текст в коде остаётся русским, переводится при показе: LocPass проходит по надписям интерфейса, Mats.Text — по надписям
// в зале. Так переводятся и строки, пришедшие с сервера (всплывающие надписи, реплики), и составные («ОЧКО · ХИБАРИ»).
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Tobe
{
    public enum Lang { Ru, En, Ja }

    public static class Loc
    {
        public static readonly string[] LangNames = { "РУССКИЙ", "ENGLISH", "日本語" };
        public static event Action Changed;
        static Lang cur; static bool loaded;
        static readonly Dictionary<string, string[]> exact = new Dictionary<string, string[]>();
        static readonly Dictionary<string, string[]> upper = new Dictionary<string, string[]>();   // «Связующий» → и «СВЯЗУЮЩИЙ»
        static readonly List<(Regex re, string[] tr)> templates = new List<(Regex, string[])>();
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        public static Lang Cur { get { Load(); return cur; } }

        public static void Set(Lang l)
        {
            Load();
            if (l == cur) return;
            cur = l; cache.Clear();
            try { PlayerPrefs.SetInt("tobe.lang", (int)l); PlayerPrefs.Save(); } catch (Exception) { }
            Changed?.Invoke();
        }

        static void Load()
        {
            if (loaded) return; loaded = true;
            int def = Application.systemLanguage == SystemLanguage.Russian || Application.systemLanguage == SystemLanguage.Ukrainian || Application.systemLanguage == SystemLanguage.Belarusian ? 0
                : Application.systemLanguage == SystemLanguage.Japanese ? 2 : 1;
            try { def = PlayerPrefs.GetInt("tobe.lang", def); } catch (Exception) { }
            cur = (Lang)Mathf.Clamp(def, 0, 2);
            var ta = Resources.Load<TextAsset>("Loc/strings");
            if (ta == null) { Debug.LogWarning("[Tobe] нет таблицы перевода Resources/Loc/strings"); return; }
            var tpl = new List<(int lit, Regex re, string[] tr)>();
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                var c = line.Split('\t');
                if (c.Length < 3) continue;
                for (int i = 0; i < 3; i++) c[i] = c[i].Replace("\\n", "\n");
                if (c[0].Contains("{0}"))
                {   // шаблон: литералы точно, {n} — любая часть (сама переводится, если есть в таблице)
                    string pat = "^" + Regex.Replace(Regex.Escape(c[0]), @"\\\{(\d)}", "(?<p$1>.+?)") + "$";
                    tpl.Add((Regex.Replace(c[0], @"\{\d\}", "").Length, new Regex(pat, RegexOptions.Singleline), c));
                }
                else
                {
                    exact[c[0]] = c;
                    string u = c[0].ToUpperInvariant();
                    if (u != c[0] && !upper.ContainsKey(u)) upper[u] = new[] { u, c[1].ToUpperInvariant(), c[2] };
                }
            }
            tpl.Sort((a, b) => b.lit.CompareTo(a.lit));   // длинные шаблоны первыми: «ДО {0} ОЧКОВ…» раньше «ДО {0}»
            foreach (var t in tpl) templates.Add((t.re, t.tr));
        }

        /// <summary>Перевод русской строки на текущий язык (нет в таблице — как есть).</summary>
        public static string T(string ru)
        {
            Load();
            if (cur == Lang.Ru || string.IsNullOrEmpty(ru)) return ru;
            if (cache.TryGetValue(ru, out var r)) return r;
            r = Translate(ru, true);
            if (cache.Count > 4000) cache.Clear();
            cache[ru] = r;
            return r;
        }

        static string Translate(string ru, bool useTemplates)
        {
            int li = (int)cur;
            if (exact.TryGetValue(ru, out var e) || upper.TryGetValue(ru, out e)) return e[li].Length > 0 ? e[li] : e[1].Length > 0 ? e[1] : ru;
            if (!useTemplates || !HasCyrillic(ru)) return ru;
            foreach (var (re, tr) in templates)
            {
                var m = re.Match(ru);
                if (!m.Success) continue;
                string outS = tr[li].Length > 0 ? tr[li] : tr[1];
                for (int k = 0; k < 4; k++)
                {
                    var g = m.Groups["p" + k];
                    if (g.Success) outS = outS.Replace("{" + k + "}", Translate(g.Value, false));
                }
                return outS;
            }
            return ru;
        }

        static bool HasCyrillic(string s) { foreach (char ch in s) if (ch >= 'Ѐ' && ch <= 'ӿ') return true; return false; }

        // ---------- надписи в мире (TextMesh): помним исходник и рамку, при смене языка — перевод и подгонка
        struct World { public TextMesh tm; public string src; public float size, fitW, fitH; }
        static readonly List<World> world = new List<World>();

        public static void TrackWorld(TextMesh tm, string src, float fitW, float fitH)
        {
            var w = new World { tm = tm, src = src, size = tm.characterSize, fitW = fitW, fitH = fitH };
            world.Add(w); ApplyWorld(w);
            if (world.Count == 1) Changed += () => { world.RemoveAll(x => x.tm == null); foreach (var x in world) ApplyWorld(x); };
        }

        static void ApplyWorld(World w)
        {
            w.tm.characterSize = w.size;
            w.tm.text = T(w.src);
            if (w.fitW <= 0f) return;
            var r = w.tm.GetComponent<MeshRenderer>(); if (r == null) return;
            Vector3 b = r.localBounds.size;   // размер считается сразу при смене текста
            if (b.x < 1e-4f || b.y < 1e-4f) return;
            w.tm.characterSize = w.size * Mathf.Min(w.fitW / b.x, w.fitH / b.y);
        }
    }

    /// <summary>Переводит все надписи (UI Text) под своим объектом: запоминает исходник каждой надписи и переводит, когда код
    /// меняет текст. Работает в конце кадра, до отрисовки интерфейса — без мигания русского.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class LocPass : MonoBehaviour
    {
        readonly List<Text> texts = new List<Text>(256);
        readonly Dictionary<Text, bool> input = new Dictionary<Text, bool>();
        readonly Dictionary<Text, (string src, string shown)> seen = new Dictionary<Text, (string, string)>();
        bool dirty;

        void OnEnable() { Loc.Changed += OnLang; }
        void OnDisable() { Loc.Changed -= OnLang; }
        void OnLang() { dirty = true; }

        void LateUpdate()
        {
            GetComponentsInChildren(true, texts);
            bool ru = Loc.Cur == Lang.Ru;
            foreach (var t in texts)
            {
                if (!input.TryGetValue(t, out bool typed)) { var f = t.GetComponentInParent<InputField>(true); input[t] = typed = f != null && f.textComponent == t; }
                if (typed) continue;   // то, что игрок вводит сам (ник, код), не переводим
                string s = t.text;
                if (seen.TryGetValue(t, out var e))
                {
                    if (s == e.shown && !dirty) continue;
                    if (s != e.shown) e.src = s;   // код поставил новый текст
                }
                else e.src = s;
                string tr = ru ? e.src : Loc.T(e.src);
                if (!ReferenceEquals(tr, s) && tr != s) t.text = tr;
                seen[t] = (e.src, tr);
            }
            if (seen.Count > texts.Count * 2 + 64) { var dead = new List<Text>(); foreach (var k in seen.Keys) if (k == null) dead.Add(k); foreach (var k in dead) { seen.Remove(k); input.Remove(k); } }
            dirty = false;
        }
    }
}
