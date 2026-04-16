using System.Text;

namespace YouTubeDownloader.Models.Services.Arguments;

public class CommandBuilder {
    private readonly List<string> _args = new();
    private string? _url;

    public CommandBuilder Verbose() => Add("--verbose");
    public CommandBuilder NoWarnings() => Add("--no-warnings");
    public CommandBuilder Progress() => Add("--progress");
    public CommandBuilder Newline() => Add("--newline");
    public CommandBuilder Json() => Add("-J");
    public CommandBuilder Format(string formatId) => Add($"-f {formatId}");
    public CommandBuilder Output(string outputTemplate) => Add($"-o \"{outputTemplate}\"");
    public CommandBuilder Url(string url) { _url = url; return this; }

    private CommandBuilder Add(string arg) { _args.Add(arg); return this; }

    public string Build() {
        if (string.IsNullOrEmpty(_url))
            throw new InvalidOperationException("URL не указан");
        return $"{string.Join(" ", _args)} \"{_url}\"";
    }

    public static CommandBuilder Create() => new();
}