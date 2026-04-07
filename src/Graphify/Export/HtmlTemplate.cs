namespace Graphify.Export;

/// <summary>
/// HTML template for interactive graph visualization using vis.js.
/// </summary>
internal static class HtmlTemplate
{
    /// <summary>
    /// Color palette for communities (matches Python implementation).
    /// </summary>
    public static readonly string[] CommunityColors =
    [
        "#4E79A7", "#F28E2B", "#E15759", "#76B7B2", "#59A14F",
        "#EDC948", "#B07AA1", "#FF9DA7", "#9C755F", "#BAB0AC"
    ];

    /// <summary>
    /// Maximum nodes allowed in HTML visualization to prevent browser performance issues.
    /// </summary>
    public const int MaxNodesForVisualization = 5000;

    /// <summary>
    /// Generate the complete HTML document with embedded data.
    /// </summary>
    public static string Generate(string title, string nodesJson, string edgesJson, string legendJson, string stats)
    {
        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>graphify - {{title}}</title>
<script src="https://unpkg.com/vis-network/standalone/umd/vis-network.min.js"></script>
{{GetStyles()}}
</head>
<body>
<div id="graph"></div>
<div id="sidebar">
  <div id="search-wrap">
    <input id="search" type="text" placeholder="Search nodes..." autocomplete="off">
    <div id="search-results"></div>
  </div>
  <div id="info-panel">
    <h3>Node Info</h3>
    <div id="info-content"><span class="empty">Click a node to inspect it</span></div>
  </div>
  <div id="legend-wrap">
    <h3>Communities</h3>
    <div id="legend"></div>
  </div>
  <div id="stats">{{stats}}</div>
</div>
{{GetScript(nodesJson, edgesJson, legendJson)}}
</body>
</html>
""";
    }

    private static string GetStyles()
    {
        return """
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { background: #0f0f1a; color: #e0e0e0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; display: flex; height: 100vh; overflow: hidden; }
  #graph { flex: 1; }
  #sidebar { width: 280px; background: #1a1a2e; border-left: 1px solid #2a2a4e; display: flex; flex-direction: column; overflow: hidden; }
  #search-wrap { padding: 12px; border-bottom: 1px solid #2a2a4e; }
  #search { width: 100%; background: #0f0f1a; border: 1px solid #3a3a5e; color: #e0e0e0; padding: 7px 10px; border-radius: 6px; font-size: 13px; outline: none; }
  #search:focus { border-color: #4E79A7; }
  #search-results { max-height: 140px; overflow-y: auto; padding: 4px 12px; border-bottom: 1px solid #2a2a4e; display: none; }
  .search-item { padding: 4px 6px; cursor: pointer; border-radius: 4px; font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .search-item:hover { background: #2a2a4e; }
  #info-panel { padding: 14px; border-bottom: 1px solid #2a2a4e; min-height: 140px; }
  #info-panel h3 { font-size: 13px; color: #aaa; margin-bottom: 8px; text-transform: uppercase; letter-spacing: 0.05em; }
  #info-content { font-size: 13px; color: #ccc; line-height: 1.6; }
  #info-content .field { margin-bottom: 5px; }
  #info-content .field b { color: #e0e0e0; }
  #info-content .empty { color: #555; font-style: italic; }
  .neighbor-link { display: block; padding: 2px 6px; margin: 2px 0; border-radius: 3px; cursor: pointer; font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; border-left: 3px solid #333; }
  .neighbor-link:hover { background: #2a2a4e; }
  #neighbors-list { max-height: 160px; overflow-y: auto; margin-top: 4px; }
  #legend-wrap { flex: 1; overflow-y: auto; padding: 12px; }
  #legend-wrap h3 { font-size: 13px; color: #aaa; margin-bottom: 10px; text-transform: uppercase; letter-spacing: 0.05em; }
  .legend-item { display: flex; align-items: center; gap: 8px; padding: 4px 0; cursor: pointer; border-radius: 4px; font-size: 12px; }
  .legend-item:hover { background: #2a2a4e; padding-left: 4px; }
  .legend-item.dimmed { opacity: 0.35; }
  .legend-dot { width: 12px; height: 12px; border-radius: 50%; flex-shrink: 0; }
  .legend-label { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .legend-count { color: #666; font-size: 11px; }
  #stats { padding: 10px 14px; border-top: 1px solid #2a2a4e; font-size: 11px; color: #555; }
</style>
""";
    }

    private static string GetScript(string nodesJson, string edgesJson, string legendJson)
    {
        return $$"""
<script>
const RAW_NODES = {{nodesJson}};
const RAW_EDGES = {{edgesJson}};
const LEGEND = {{legendJson}};

// Build vis datasets
const nodesDS = new vis.DataSet(RAW_NODES.map(n => ({
  id: n.id, label: n.label, color: n.color, size: n.size,
  font: n.font, title: n.title,
  _community: n.community, _community_name: n.community_name,
  _source_file: n.source_file, _file_type: n.file_type, _degree: n.degree,
})));

const edgesDS = new vis.DataSet(RAW_EDGES.map((e, i) => ({
  id: i, from: e.from, to: e.to,
  label: '',
  title: e.title,
  dashes: e.dashes,
  width: e.width,
  color: e.color,
  arrows: { to: { enabled: true, scaleFactor: 0.5 } },
})));

const container = document.getElementById('graph');
const network = new vis.Network(container, { nodes: nodesDS, edges: edgesDS }, {
  physics: {
    enabled: true,
    solver: 'forceAtlas2Based',
    forceAtlas2Based: {
      gravitationalConstant: -60,
      centralGravity: 0.005,
      springLength: 120,
      springConstant: 0.08,
      damping: 0.4,
      avoidOverlap: 0.8,
    },
    stabilization: { iterations: 200, fit: true },
  },
  interaction: {
    hover: true,
    tooltipDelay: 100,
    hideEdgesOnDrag: true,
    navigationButtons: false,
    keyboard: false,
  },
  nodes: { shape: 'dot', borderWidth: 1.5 },
  edges: { smooth: { type: 'continuous', roundness: 0.2 }, selectionWidth: 3 },
});

network.once('stabilizationIterationsDone', () => {
  network.setOptions({ physics: { enabled: false } });
});

function showInfo(nodeId) {
  const n = nodesDS.get(nodeId);
  if (!n) return;
  const info = document.getElementById('info-content');
  info.textContent = '';

  function addField(label, value) {
    const div = document.createElement('div');
    div.className = 'field';
    if (label) { const b = document.createElement('b'); b.textContent = label; div.appendChild(b); }
    const t = document.createTextNode(value); div.appendChild(t);
    info.appendChild(div);
  }

  const labelDiv = document.createElement('div');
  labelDiv.className = 'field';
  const labelB = document.createElement('b');
  labelB.textContent = n.label;
  labelDiv.appendChild(labelB);
  info.appendChild(labelDiv);

  addField('Type: ', n._file_type || 'unknown');
  addField('Community: ', n._community_name);
  addField('Source: ', n._source_file || '-');
  addField('Degree: ', String(n._degree));

  const neighborIds = network.getConnectedNodes(nodeId);
  if (neighborIds.length) {
    const hdr = document.createElement('div');
    hdr.className = 'field'; hdr.style.marginTop = '8px'; hdr.style.color = '#aaa'; hdr.style.fontSize = '11px';
    hdr.textContent = 'Neighbors (' + neighborIds.length + ')';
    info.appendChild(hdr);
    const list = document.createElement('div');
    list.id = 'neighbors-list';
    neighborIds.forEach(nid => {
      const nb = nodesDS.get(nid);
      const span = document.createElement('span');
      span.className = 'neighbor-link';
      span.style.borderLeftColor = nb ? nb.color.background : '#555';
      span.textContent = nb ? nb.label : nid;
      span.onclick = () => focusNode(nid);
      list.appendChild(span);
    });
    info.appendChild(list);
  }
}

function focusNode(nodeId) {
  network.focus(nodeId, { scale: 1.4, animation: true });
  network.selectNodes([nodeId]);
  showInfo(nodeId);
}

network.on('click', params => {
  if (params.nodes.length > 0) showInfo(params.nodes[0]);
  else document.getElementById('info-content').innerHTML = '<span class="empty">Click a node to inspect it</span>';
});

const searchInput = document.getElementById('search');
const searchResults = document.getElementById('search-results');
searchInput.addEventListener('input', () => {
  const q = searchInput.value.toLowerCase().trim();
  searchResults.innerHTML = '';
  if (!q) { searchResults.style.display = 'none'; return; }
  const matches = RAW_NODES.filter(n => n.label.toLowerCase().includes(q)).slice(0, 20);
  if (!matches.length) { searchResults.style.display = 'none'; return; }
  searchResults.style.display = 'block';
  matches.forEach(n => {
    const el = document.createElement('div');
    el.className = 'search-item';
    el.textContent = n.label;
    el.style.borderLeft = `3px solid ${n.color.background}`;
    el.style.paddingLeft = '8px';
    el.onclick = () => {
      network.focus(n.id, { scale: 1.5, animation: true });
      network.selectNodes([n.id]);
      showInfo(n.id);
      searchResults.style.display = 'none';
      searchInput.value = '';
    };
    searchResults.appendChild(el);
  });
});
document.addEventListener('click', e => {
  if (!searchResults.contains(e.target) && e.target !== searchInput)
    searchResults.style.display = 'none';
});

const hiddenCommunities = new Set();
const legendEl = document.getElementById('legend');
LEGEND.forEach(c => {
  const item = document.createElement('div');
  item.className = 'legend-item';
  const dot = document.createElement('div');
  dot.className = 'legend-dot'; dot.style.background = c.color;
  const lbl = document.createElement('span');
  lbl.className = 'legend-label'; lbl.textContent = c.label;
  const cnt = document.createElement('span');
  cnt.className = 'legend-count'; cnt.textContent = c.count;
  item.appendChild(dot); item.appendChild(lbl); item.appendChild(cnt);
  item.onclick = () => {
    if (hiddenCommunities.has(c.cid)) {
      hiddenCommunities.delete(c.cid);
      item.classList.remove('dimmed');
    } else {
      hiddenCommunities.add(c.cid);
      item.classList.add('dimmed');
    }
    const updates = RAW_NODES
      .filter(n => n.community === c.cid)
      .map(n => ({ id: n.id, hidden: hiddenCommunities.has(c.cid) }));
    nodesDS.update(updates);
  };
  legendEl.appendChild(item);
});
</script>
""";
    }
}
