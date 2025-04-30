import dagre from "@dagrejs/dagre";
import { DefaultEdge, Node, Position, useVueFlow } from "@vue-flow/core";
import { ref } from "vue";

export function useLayout() {
  const { findNode } = useVueFlow();

  const graph = ref(new dagre.graphlib.Graph());

  function layout(nodes: Node[], edges: DefaultEdge[]) {
    // we create a new graph instance, in case some nodes/edges were removed, otherwise dagre would act as if they were still there
    const dagreGraph = new dagre.graphlib.Graph();

    graph.value = dagreGraph;

    dagreGraph.setDefaultEdgeLabel(() => ({}));

    const isHorizontal = false;
    dagreGraph.setGraph({ rankdir: "TB" });

    for (const node of nodes) {
      const graphNode = findNode(node.id);
      if (graphNode === undefined) continue;

      dagreGraph.setNode(node.id, { width: graphNode.dimensions.width || 250, height: graphNode.dimensions.height || 55 });
    }

    for (const edge of edges) {
      dagreGraph.setEdge(edge.source, edge.target);
    }

    dagre.layout(dagreGraph);

    // set nodes with updated positions
    return nodes.map((node) => {
      const nodeWithPosition = dagreGraph.node(node.id);

      return {
        ...node,
        targetPosition: isHorizontal ? Position.Left : Position.Top,
        sourcePosition: isHorizontal ? Position.Right : Position.Bottom,
        position: { x: nodeWithPosition.x, y: nodeWithPosition.y },
      };
    });
  }

  return { graph, layout };
}
