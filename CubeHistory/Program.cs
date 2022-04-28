// See https://aka.ms/new-console-template for more information

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json;

Console.WriteLine("Hello, World!");

using var httpClient = new HttpClient()
{
    BaseAddress = new Uri("http://cubecobra.com/")
};

var cubeId = "modernclassics";// args.AsQueryable().FirstOrDefault();

var cubeHistory = new CubeHistory();
var changes = new List<Change>();

for(var i = 0; i<9; i++)
{
    var response = await httpClient.GetAsync($"cube/blog/{cubeId}/{i}");

    if (!response.IsSuccessStatusCode)
        break;
    
    var parser = new HtmlParser();

    var doc  = await parser.ParseDocumentAsync(response.Content.ReadAsStream());
    
    changes.AddRange(GetCardElements(doc, GetChanges));
    
    foreach (var slot in GetCardElements(doc, GetSlots))
    {
        cubeHistory.AddSlot(slot);
    }
}

changes.Reverse();

foreach (var change in changes)
{
    cubeHistory.MakeChange(change);
}

cubeHistory.PrintStats();


IEnumerable<Change> GetChanges(INode? node)
{
    return GetThing(node, '→', Change.FromPair);
}

IEnumerable<Slot> GetSlots(INode? node)
{
    return GetThing<Slot>(node, '+', Slot.FromString);
}

IEnumerable<T> GetThing<T>(INode? node, char separator, Func<string, T?> factory)
{
    if (node is IHtmlDocument || node is not IText { } text) return Enumerable.Empty<T>();
    
    var html = text.ToHtml()["window.reactProps = ".Length..].TrimEnd(';');

    if (string.IsNullOrEmpty(html))
        return Enumerable.Empty<T>();

    try
    {
        var page = JsonConvert.DeserializeObject<Page>(html);
        
        var list = new List<T>();

        foreach (var post in page.Posts)
        {
            var p = new HtmlParser();

            var things = p.ParseDocument(post.Changelist).Body.Text()
                .Split(separator, StringSplitOptions.RemoveEmptyEntries);

            list.AddRange(things
                .Select(factory)
                .Where(r => r != null)!
                );
        }

        return list;

    }
    catch
    {
        return Enumerable.Empty<T>();
    }
}


static IEnumerable<T> GetCardElements<T>(IHtmlDocument document, Func<INode, IEnumerable<T>> selector)
{
    return NextLevel(document.GetRoot(), selector).ToList();
    
    List<T> NextLevel(INode node, Func<INode, IEnumerable<T>> selector, List<T>? all =null)
    {
        all ??= new List<T>();

        all.AddRange(selector(node));
        
        foreach (var child in node.ChildNodes.Where(c => c is not null))
        {
            NextLevel(child, selector, all);
        }
        
        return all;
    }
}


record Change(string From, string To)
{
    public static Change? FromPair(string str)
    {
        var s = str.Split('>', StringSplitOptions.RemoveEmptyEntries);

        return s.Length != 2 
            ? null 
            : new Change(s[0].Trim(), s[1].Trim());
    }
}

internal class Slot
{
    public static Slot? FromString(string str)
    {
        return str.Contains('>') 
            ? null : 
            new Slot(str.Trim());
    }
    
    private readonly Stack<string> _history = new();

    private Slot(string card)
    {
        _history.Push(card);
    }

    public string Current => _history.Peek();
    public int Variations => _history.Count;
    public string History => string.Join(" > ", GetHistory());

    public void Update(string card)
    {
        _history.Push(card);
    }

    private IEnumerable<string> GetHistory() => _history;
}

internal class CubeHistory
{
    private readonly List<Slot> _slots = new ();

    public void AddSlot(Slot slot)
    {
        _slots.Add(slot);
    }

    public void MakeChange(Change change)
    {
        var slot = _slots.FirstOrDefault(s => s.Current == change.From);

        if (slot is null)
        {
            throw new Exception($"Invalid change from {change.From} to {change.To}");
        }
        
        slot.Update(change.To);
    }

    public void PrintStats()
    {
        var mostVariedSLot = _slots.MaxBy(s => s.Variations); 
        
        Console.WriteLine($"Most varied: {mostVariedSLot.Variations}, {mostVariedSLot.History}");

        var nUnchangedSlots = _slots.Count(s => s.Variations == 1);
        
        Console.WriteLine($"{nUnchangedSlots} slots have never changed");
    }
}

internal record Page(Post[] Posts);
internal record Post(string Changelist);



