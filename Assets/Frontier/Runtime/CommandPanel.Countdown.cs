using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform countdownCard;
        private Text countdownLabel;
        private readonly List<GameObject> countdownHidden = new List<GameObject>();
        private readonly Image[] countdownSteps = new Image[3];
        private bool countdownVisible;

        private bool UpdateCountdownPresentation()
        {
            if (!Session.IsCountingDown || Quest.Placing)
            {
                if (countdownVisible) { RestoreCountdownPresentation(); Rebuild(); }
                return false;
            }
            if (countdownCard == null)
            {
                countdownCard = Section(root, "Match countdown", Vector2.zero, new Vector2(420, 380), ConsoleTheme.Background);
                countdownCard.anchorMin = countdownCard.anchorMax = countdownCard.pivot = Vector2.one * .5f;
                countdownCard.anchoredPosition = Vector2.zero;
                var title = Label(countdownCard, "Match starts in", new Vector2(28, -38), new Vector2(364, 40), 26, ConsoleTheme.Muted, TextAnchor.MiddleCenter);
                title.fontStyle = FontStyle.Bold;
                countdownLabel = Label(countdownCard, "3", new Vector2(28, -90), new Vector2(364, 204), 156, ConsoleTheme.Ink, TextAnchor.MiddleCenter);
                countdownLabel.fontStyle = FontStyle.Bold;
                for (int i = 0; i < countdownSteps.Length; i++)
                    countdownSteps[i] = Section(countdownCard, "Count " + (i + 1), new Vector2(118 + i * 64, -324), new Vector2(56, 8), ConsoleTheme.Accent).GetComponent<Image>();
            }
            if (!countdownVisible)
            {
                CancelPointerInteraction();
                countdownHidden.Clear();
                foreach (Transform child in root)
                {
                    if (child == countdownCard || !child.gameObject.activeSelf) continue;
                    countdownHidden.Add(child.gameObject); child.gameObject.SetActive(false);
                }
                root.GetComponent<Image>().color = Color.clear;
                countdownVisible = true;
                countdownCard.gameObject.SetActive(true);
            }
            countdownLabel.text = Session.CountdownNumber.ToString();
            for (int i = 0; i < countdownSteps.Length; i++)
                countdownSteps[i].color = i < Session.CountdownNumber ? ConsoleTheme.Accent : ConsoleTheme.Card;
            return true;
        }

        private void RestoreCountdownPresentation()
        {
            if (!countdownVisible) return;
            countdownVisible = false;
            foreach (var child in countdownHidden) if (child != null) child.SetActive(true);
            countdownHidden.Clear();
            countdownCard.gameObject.SetActive(false);
            root.GetComponent<Image>().color = background;
        }
    }
}
