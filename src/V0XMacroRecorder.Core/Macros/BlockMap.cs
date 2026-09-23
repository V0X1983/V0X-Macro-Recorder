namespace V0XMacroRecorder.Core.Macros;

/// <summary>
/// Résout, une seule fois par lecture, l'appariement des blocs (Si/Sinon/Fin si, Boucle/Fin boucle) et les
/// étiquettes d'une liste de commandes, pour permettre au moteur de sauter (voir <c>MacroPlayer</c>/<c>ExecutionSignal</c>
/// dans <c>V0XMacroRecorder.Core.Playback</c>). Construit par <see cref="Build"/> ; lève <see cref="MacroFormatException"/>
/// sur un fichier corrompu ou modifié à la main de façon incohérente (bloc mal fermé, étiquette dupliquée).
/// </summary>
public sealed class BlockMap
{
    private readonly Dictionary<int, int> _matchingEnd = [];
    private readonly Dictionary<int, int> _matchingStart = [];
    private readonly Dictionary<int, int> _elseIndex = [];
    private readonly Dictionary<int, int> _elseToEnd = [];
    private readonly Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);

    private BlockMap()
    {
    }

    /// <summary>Index de début de bloc (Si/Boucle) → index de sa fin (Fin si/Fin boucle).</summary>
    public IReadOnlyDictionary<int, int> MatchingEnd => _matchingEnd;

    /// <summary>Index de fin de bloc → index de son début (sens inverse de <see cref="MatchingEnd"/>).</summary>
    public IReadOnlyDictionary<int, int> MatchingStart => _matchingStart;

    /// <summary>Index d'un <c>IfCommand</c> → index de son <c>ElseCommand</c>, uniquement s'il en a un.</summary>
    public IReadOnlyDictionary<int, int> ElseIndex => _elseIndex;

    /// <summary>Index d'un <c>ElseCommand</c> → index de son <c>EndIfCommand</c>.</summary>
    public IReadOnlyDictionary<int, int> ElseToEnd => _elseToEnd;

    /// <summary>Nom d'étiquette (insensible à la casse) → index du <c>LabelCommand</c>.</summary>
    public IReadOnlyDictionary<string, int> Labels => _labels;

    public static BlockMap Build(IReadOnlyList<MacroCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var map = new BlockMap();
        var stack = new Stack<(string Kind, int Index)>();

        for (var i = 0; i < commands.Count; i++)
        {
            switch (commands[i])
            {
                case IfCommand:
                    stack.Push(("if", i));
                    break;

                case LoopCommand:
                    stack.Push(("loop", i));
                    break;

                case ElseCommand:
                    if (stack.Count == 0 || stack.Peek().Kind != "if")
                    {
                        throw new MacroFormatException("« Sinon » sans « Si » correspondant.");
                    }

                    map._elseIndex[stack.Peek().Index] = i;
                    break;

                case EndIfCommand:
                    if (stack.Count == 0 || stack.Peek().Kind != "if")
                    {
                        throw new MacroFormatException("« Fin si » sans « Si » correspondant.");
                    }

                    CloseBlock(map, stack, i, linkElse: true);
                    break;

                case EndLoopCommand:
                    if (stack.Count == 0 || stack.Peek().Kind != "loop")
                    {
                        throw new MacroFormatException("« Fin boucle » sans « Boucle » correspondante.");
                    }

                    CloseBlock(map, stack, i, linkElse: false);
                    break;

                case LabelCommand label:
                    if (!map._labels.TryAdd(label.Name, i))
                    {
                        throw new MacroFormatException($"Étiquette dupliquée : « {label.Name} ».");
                    }

                    break;
            }
        }

        if (stack.Count > 0)
        {
            var (kind, _) = stack.Peek();
            throw new MacroFormatException(kind == "if"
                ? "« Si » sans « Fin si » correspondant."
                : "« Boucle » sans « Fin boucle » correspondante.");
        }

        return map;
    }

    private static void CloseBlock(BlockMap map, Stack<(string Kind, int Index)> stack, int endIndex, bool linkElse)
    {
        var start = stack.Pop().Index;
        map._matchingEnd[start] = endIndex;
        map._matchingStart[endIndex] = start;
        if (linkElse && map._elseIndex.TryGetValue(start, out var elseIdx))
        {
            map._elseToEnd[elseIdx] = endIndex;
        }
    }
}
