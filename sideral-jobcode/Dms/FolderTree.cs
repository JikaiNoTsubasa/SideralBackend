using sideral_context.Dms;

namespace sideral_jobcode.Dms;

public record FolderNode(long Id, long? ParentId, string Name, bool InheritPermissions, bool Deleted);

/// <summary>
/// Arbre complet des dossiers (colonnes légères) et droits d'un utilisateur, chargés une fois par requête.
/// Droit effectif sur un dossier = max(droits propres, droit effectif du parent si héritage activé).
/// Un administrateur est gestionnaire de tous les dossiers.
/// </summary>
public class FolderTree
{
    private readonly Dictionary<long, FolderNode> _nodes;
    private readonly ILookup<long?, FolderNode> _children;
    private readonly Dictionary<long, PermissionLevel> _ownLevels;
    private readonly Dictionary<long, PermissionLevel> _levels = [];

    public bool IsAdmin { get; }

    public FolderTree(IEnumerable<FolderNode> nodes, Dictionary<long, PermissionLevel> ownLevels, bool isAdmin)
    {
        _nodes = nodes.ToDictionary(n => n.Id);
        _children = _nodes.Values.ToLookup(n => n.ParentId);
        _ownLevels = ownLevels;
        IsAdmin = isAdmin;
    }

    public FolderNode? Find(long id) => _nodes.GetValueOrDefault(id);

    /// <summary>Droit effectif, indépendamment de la corbeille.</summary>
    public PermissionLevel Level(long id)
    {
        if (IsAdmin)
            return PermissionLevel.Manage;
        if (_levels.TryGetValue(id, out var cached))
            return cached;
        if (!_nodes.TryGetValue(id, out var node))
            return PermissionLevel.None;

        var level = _ownLevels.GetValueOrDefault(id);
        if (node.InheritPermissions && node.ParentId is { } parentId)
        {
            var inherited = Level(parentId);
            if (inherited > level)
                level = inherited;
        }
        _levels[id] = level;
        return level;
    }

    /// <summary>Vrai si le dossier ou l'un de ses ancêtres est dans la corbeille.</summary>
    public bool IsDeleted(long id)
    {
        for (var node = Find(id); node is not null; node = node.ParentId is { } p ? Find(p) : null)
        {
            if (node.Deleted)
                return true;
        }
        return false;
    }

    /// <summary>Vrai si l'un des ancêtres (hors dossier lui-même) est dans la corbeille.</summary>
    public bool IsParentDeleted(long id) => Find(id)?.ParentId is { } parentId && IsDeleted(parentId);

    /// <summary>Dossier existant, hors corbeille, avec au moins le droit demandé.</summary>
    public bool Can(long id, PermissionLevel level) => _nodes.ContainsKey(id) && !IsDeleted(id) && Level(id) >= level;

    /// <summary>Ancêtres de la racine jusqu'au dossier inclus.</summary>
    public List<FolderNode> Ancestors(long id)
    {
        var path = new List<FolderNode>();
        for (var node = Find(id); node is not null; node = node.ParentId is { } p ? Find(p) : null)
        {
            path.Add(node);
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Fil d'Ariane visible par l'utilisateur : la portion du chemin qu'il peut lire, en partant du dossier
    /// (un utilisateur qui n'a accès qu'à un sous-dossier ne voit pas les dossiers au-dessus).
    /// </summary>
    public List<FolderNode> Breadcrumb(long id)
    {
        var path = Ancestors(id);
        var start = path.Count - 1;
        while (start > 0 && Level(path[start - 1].Id) >= PermissionLevel.Read)
        {
            start--;
        }
        return path[start..];
    }

    /// <summary>Chemin complet lisible ("Projets / 2026 / Contrats"), pour les messages et la corbeille.</summary>
    public string PathOf(long id) => string.Join(" / ", Ancestors(id).Select(n => n.Name));

    /// <summary>
    /// Sous-dossiers visibles (hors corbeille). À la racine : les dossiers racine pour un administrateur,
    /// sinon les "points d'entrée" (dossiers lisibles dont le parent ne l'est pas).
    /// </summary>
    public IEnumerable<FolderNode> VisibleChildren(long? parentId)
    {
        if (parentId is not null)
        {
            return _children[parentId].Where(n => !n.Deleted && Level(n.Id) >= PermissionLevel.Read);
        }
        if (IsAdmin)
        {
            return _children[null].Where(n => !n.Deleted);
        }
        return _nodes.Values.Where(n =>
            Level(n.Id) >= PermissionLevel.Read &&
            !IsDeleted(n.Id) &&
            (n.ParentId is null || Level(n.ParentId.Value) < PermissionLevel.Read));
    }

    /// <summary>Le dossier et tous ses descendants (y compris ceux dans la corbeille).</summary>
    public List<long> SelfAndDescendants(long id)
    {
        var result = new List<long>();
        var stack = new Stack<long>([id]);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            result.Add(current);
            foreach (var child in _children[current])
            {
                stack.Push(child.Id);
            }
        }
        return result;
    }

    public IEnumerable<FolderNode> Children(long? parentId) => _children[parentId];
}
