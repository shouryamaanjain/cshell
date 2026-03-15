using CShell.Core.Models;

namespace CShell.Services;

public sealed class WorkspaceManager
{
    private readonly List<Workspace> _workspaces = new();
    public IReadOnlyList<Workspace> Workspaces => _workspaces;
    public Workspace? SelectedWorkspace { get; set; }

    public Workspace CreateWorkspace(string title = "Terminal")
    {
        var ws = new Workspace { Title = title };
        _workspaces.Add(ws);
        SelectedWorkspace = ws;
        return ws;
    }

    public void RemoveWorkspace(Guid id)
    {
        var ws = _workspaces.Find(w => w.Id == id);
        if (ws != null)
        {
            foreach (var panel in ws.Panels.Values)
                panel.Close();
            _workspaces.Remove(ws);
            if (SelectedWorkspace == ws)
                SelectedWorkspace = _workspaces.Count > 0 ? _workspaces[^1] : null;
        }
    }
}
