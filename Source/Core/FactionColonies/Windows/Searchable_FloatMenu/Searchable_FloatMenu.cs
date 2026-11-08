using UnityEngine;

namespace FactionColonies;

class Searchable_FloatMenu : FloatMenu
{
    private readonly List<FloatMenuOption> filteredOptions;
    private readonly char ignoreBeforeChar;
    private readonly bool useIgnoreBeforeChar;

    private string searchTerm = "";
    private Color baseColor = Color.white;

    private readonly Vector2 SearchBarOffset = new(25f, -27f);

    /// <summary>
    /// Calculates the required Width dynamically
    /// </summary>
    private float ColumnWidth
    {
        get
        {
            float num = 70f;
            for (int i = 0; i < options.Count; i++)
            {
                float requiredWidth = options[i].RequiredWidth;
                if (requiredWidth >= 300f)
                {
                    return 300f;
                }
                if (requiredWidth > num)
                {
                    num = requiredWidth;
                }
            }
            return Mathf.Round(num);
        }
    }

    /// <summary>
    ///	Like a normal <c>FloatMenu</c>, but searchable!
    /// </summary>
    /// <param name="options"></param>
    /// <param name="ignoreBeforeChar"></param>
    /// <param name="useIgnoreBeforeChar"></param>
    public Searchable_FloatMenu(List<FloatMenuOption> options, bool useIgnoreBeforeChar = true, char ignoreBeforeChar = '-') : base(options)
    {
        this.useIgnoreBeforeChar = useIgnoreBeforeChar;
        this.ignoreBeforeChar = ignoreBeforeChar;

        filteredOptions = options.ListFullCopy();
    }

    /// <summary>
    /// Adds the search bar and search bar functionality
    /// </summary>
    public override void ExtraOnGUI()
    {
        base.ExtraOnGUI();
        Vector2 vector = new(windowRect.x, windowRect.y);
        Text.Font = GameFont.Small;
        Rect searchRect = new(vector.x - SearchBarOffset.x, vector.y + SearchBarOffset.y, ColumnWidth + SearchBarOffset.x, 27f);

        Find.WindowStack.ImmediateWindow(6830963, searchRect, WindowLayer.Super, delegate
        {
            GUI.color = baseColor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            //Rect position = searchRect.AtZero();
            Rect bgRect = searchRect.AtZero();

            bgRect.width -= SearchBarOffset.x;
            bgRect.x += SearchBarOffset.x;

            Rect searchBarRect = bgRect.ContractedBy(2f);
            Rect labelRect = bgRect.ContractedBy(2f);

            //position.width = 150f;
            //GUI.DrawTexture(position, TexUI.TextBGBlack);

            Widgets.DrawWindowBackground(bgRect);

            searchTerm = Widgets.TextField(searchBarRect, searchTerm);

            options.Clear();
            if (searchTerm != "")
            {
                vanishIfMouseDistant = false;
                if (useIgnoreBeforeChar)
                {
                    options.AddRange(filteredOptions.Where(option =>
                    {
                        // Strip the trailing suffix after the last ignoreBeforeChar (e.g. " - $cost")
                        // while preserving any dashes inside the name itself. No delimiter -> search whole label.
                        int cut = option.Label.LastIndexOf(ignoreBeforeChar);
                        string searchable = (cut < 0 ? option.Label : option.Label.Substring(0, cut)).ToLower();
                        return searchable.Contains(searchTerm.ToLower());
                    }));
                }
                else
                {
                    options.AddRange(filteredOptions.Where(option => option.Label.ToLower().Contains(searchTerm.ToLower())));
                }
            }
            else
            {
                UIUtil.ClampedLabel(labelRect, $" {"FCFloatMenuSearchable".Translate()}");
                options = filteredOptions.ToList();
                vanishIfMouseDistant = true;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }, false, false, 0f);
    }
}
