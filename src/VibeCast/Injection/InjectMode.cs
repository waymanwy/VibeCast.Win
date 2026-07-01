namespace VibeCast.Injection;

/// <summary>How text is written into the focused control.</summary>
public enum InjectMode
{
    /// <summary>Insert at the caret (append). The default — safe for any control.</summary>
    Editor = 0,

    /// <summary>Select-all then replace. Useful for chat / prompt boxes with stale content.</summary>
    Dialog = 1
}
