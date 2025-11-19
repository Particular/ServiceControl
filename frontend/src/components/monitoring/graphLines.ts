import { ref, toValue, watchEffect } from "vue";
import type { Coordinate, PlotData } from "./PlotData";

export function useGraph(plotdata: () => PlotData | undefined, minimumyaxis: () => number | undefined, minPoints = () => 10) {
  const valuesPath = ref(""),
    valuesArea = ref(""),
    maxYaxis = ref(10),
    average = ref(0),
    averageLine = ref("");

  const createGraph = () => {
    const plotData = toValue(plotdata) ?? { points: [], average: 0 };
    const values = (() => {
      let result = plotData.points;
      if (result.length === 0) {
        result = new Array(toValue(minPoints)).fill(0);
      }
      return result;
    })();
    const xTick = 100 / (values.length - 1);
    const coordinates = values.reduce((points: Coordinate[], yValue, i) => [...points, [i * xTick, yValue] as Coordinate], []);
    valuesPath.value = new Path().startAt(coordinates[0]).followCoordinates(coordinates.slice(1)).toString();
    valuesArea.value = new Path().startAt([0, 0]).followCoordinates(coordinates).lineTo([100, 0]).close().toString();

    average.value = plotData.average;
    //TODO: why is this called minimumYaxis when it's only used to determine the maxYaxis?
    // should the graph actually set the min y value rather than leave it at 0?
    const minYaxis = toValue(minimumyaxis) ?? 10;
    const minimumYaxis = !isNaN(minYaxis) ? Number(minYaxis) : 10;
    maxYaxis.value = Math.max(...[...values, average.value * 1.5, minimumYaxis]);

    averageLine.value = new Path().startAt([0, average.value]).lineTo([100, average.value]).toString();
  };

  watchEffect(() => createGraph());

  return { valuesPath, valuesArea, maxYaxis, average, averageLine };
}

class Path {
  #pathElements: string[] = [];
  #complete = false;

  startAt([x, y]: Coordinate) {
    if (this.#pathElements.length > 0) throw new Error("startAt must be the first call on a path");
    return this.moveTo([x, y]);
  }

  moveTo([x, y]: Coordinate) {
    if (this.#complete) throw new Error("Path is already closed");
    this.#pathElements.push(`M${x} ${y}`);
    return this;
  }

  lineTo([x, y]: Coordinate) {
    if (this.#complete) throw new Error("Path is already closed");
    this.#pathElements.push(`L${x} ${y}`);
    return this;
  }

  followCoordinates(coordinates: Coordinate[]) {
    for (const c of coordinates) {
      this.lineTo(c);
    }
    return this;
  }

  close() {
    if (this.#complete) throw new Error("Path is already closed");
    if (this.#pathElements.length === 0) throw new Error("Cannot close an empty path");
    this.#pathElements.push("Z");
    this.#complete = true;
    return this;
  }

  toString() {
    return this.#pathElements.join(" ");
  }
}
