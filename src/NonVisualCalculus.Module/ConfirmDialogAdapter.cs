namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Adapter: reads DE's shared confirmation/error popup into the natural-case message to speak, or null
    /// when no popup is up. The popup is <see cref="ConfirmationController"/>, a <c>LiteSingleton</c> behind
    /// quit, error, and every yes/no prompt; it runs its own NavigationGroup and sets no EventSystem
    /// selection, so the focus poller never sees it and the module watches the controller directly. The
    /// message is the controller's own text: when shown localized it keeps the I2 term, which resolves to
    /// natural case (the on-screen <c>Text</c> is uppercased for display and carries no Localize to recase
    /// from), with the format argument substituted for the formatted variant; a non-localized caller's
    /// prebuilt string is read verbatim. Extraction only; no word choice and no caching past the live read.
    /// </summary>
    public static class ConfirmDialogAdapter
    {
        public static string TryRead()
        {
            var inst = ConfirmationController.Singleton;
            if (inst == null || !inst.IsVisible)
                return null;

            if (inst.isLocalized && !string.IsNullOrEmpty(inst.localizationTerm))
            {
                string text = GameLocalization.Translate(inst.localizationTerm);
                if (!string.IsNullOrEmpty(text))
                {
                    if (inst.isFormatted && !string.IsNullOrEmpty(inst.textToApply) && text.Contains("{0}"))
                        text = text.Replace("{0}", inst.textToApply);
                    return text;
                }
            }

            return inst.Text != null ? inst.Text.text : null;
        }
    }
}
