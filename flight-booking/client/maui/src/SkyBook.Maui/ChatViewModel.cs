using System.Collections.ObjectModel;
using System.Windows.Input;
using Syncfusion.Maui.AIAssistView;

namespace SkyBook.Maui;

/// <summary>Bindable state for <see cref="SfAIAssistView"/> (React: the AIAssistViewComponent + its refs).</summary>
public sealed class ChatViewModel
{
    private AssistItem? _typing;

    public ObservableCollection<IAssistItem> AssistItems { get; } = [];

    public Profile CurrentUser { get; } = new() { Name = "You" };
    public Profile BotProfile { get; } = new() { Name = "SkyBook" };

    /// <summary>Raised when the user submits text from the built-in editor; the item is already in <see cref="AssistItems"/>.</summary>
    public event Action<string>? PromptSubmitted;

    public ICommand RequestCommand { get; }

    public ChatViewModel() => RequestCommand = new Command<object>(o =>
    {
        if (o is RequestEventArgs { RequestItem: { } item } e)
        {
            e.Handled = true; // we answer asynchronously via AddBotMessage
            PromptSubmitted?.Invoke(item.Text ?? "");
        }
    });

    public void AddUserMessage(string text) =>
        AssistItems.Add(new AssistItem { Text = text, IsRequested = true, DateTime = DateTime.Now, ShowAssistItemFooter = false });

    public void AddBotMessage(string text)
    {
        HideTyping();
        AssistItems.Add(new AssistItem { Text = text, IsRequested = false, DateTime = DateTime.Now, ShowAssistItemFooter = false });
    }

    public void ShowTyping()
    {
        if (_typing is not null) return;
        _typing = new AssistItem { Text = "…", IsRequested = false, DateTime = DateTime.Now, ShowAssistItemFooter = false };
        AssistItems.Add(_typing);
    }

    public void HideTyping()
    {
        if (_typing is null) return;
        AssistItems.Remove(_typing);
        _typing = null;
    }

    public void Clear() { _typing = null; AssistItems.Clear(); }
}
