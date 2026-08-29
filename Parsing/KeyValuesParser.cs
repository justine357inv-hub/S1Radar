namespace S1Radar.Parsing;

public sealed record KvNode(string Name, Dictionary<string,string> Values, List<KvNode> Children);

public static class KeyValuesParser
{
    public static KvNode Parse(string text)
    {
        var tokens = Tokenize(text);
        var root = new KvNode("root", new(StringComparer.OrdinalIgnoreCase), []);
        var stack = new Stack<KvNode>(); stack.Push(root);
        string? pending = null;
        for (int i=0;i<tokens.Count;i++)
        {
            var t=tokens[i];
            if(t=="{") { var n=new KvNode(pending ?? "", new(StringComparer.OrdinalIgnoreCase), []); stack.Peek().Children.Add(n); stack.Push(n); pending=null; continue; }
            if(t=="}") { if(stack.Count>1) stack.Pop(); pending=null; continue; }
            if(pending is null) { pending=t; }
            else { stack.Peek().Values[pending]=t; pending=null; }
        }
        return root;
    }

    private static List<string> Tokenize(string s)
    {
        var r=new List<string>(); int i=0;
        while(i<s.Length){ while(i<s.Length && char.IsWhiteSpace(s[i])) i++; if(i>=s.Length) break;
            if(s[i]=='/' && i+1<s.Length && s[i+1]=='/'){ while(i<s.Length && s[i]!='\n') i++; continue; }
            if(s[i]=='{'||s[i]=='}'){r.Add(s[i++].ToString());continue;}
            if(s[i]=='"'){ i++; var b=new System.Text.StringBuilder(); while(i<s.Length){ if(s[i]=='"'){i++;break;} if(s[i]=='\\'&&i+1<s.Length){b.Append(s[++i]);i++;} else b.Append(s[i++]); } r.Add(b.ToString()); }
            else { int st=i; while(i<s.Length&&!char.IsWhiteSpace(s[i])&&s[i]!='{'&&s[i]!='}') i++; r.Add(s[st..i]); }
        } return r;
    }
}
