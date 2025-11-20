<script setup lang="ts">
import { computed, ref } from "vue";
import { useFormatLargeNumber, useFormatTime } from "../../composables/formatter";
import { useGraph } from "./graphLines";
import type { PlotData } from "./PlotData";

const props = defineProps<{
  plotdata: PlotData;
  minimumyaxis?: number | undefined;
  isdurationgraph: boolean;
  metricsuffix?: string | undefined;
  type: string;
}>();

const hover = ref(false);

const { valuesPath, valuesArea, maxYaxis, average, averageLine } = useGraph(
  () => props.plotdata,
  () => props.minimumyaxis
);

const averageLabelValue = computed(() => (props.isdurationgraph ? useFormatTime(average.value).value : useFormatLargeNumber(average.value, 2)));
const averageLabelSuffix = computed(() => (props.isdurationgraph ? useFormatTime(average.value).unit.toUpperCase() : (props.metricsuffix ?? "")));
//38 is 50 (height of parent) - 6 - 6 for padding.
//To get it exact without hard coding a height value, we would need to perform measurement on the rendered SVG element, which we want to avoid
const averageLabelPosition = computed(() => `calc(${(average.value / maxYaxis.value) * 38}px - 1em)`);
</script>

<template>
  <div class="graph pull-left ng-isolate-scope" :class="[hover ? 'hover' : '']" @mouseover="hover = true" @mouseout="hover = false">
    <div class="padding">
      <svg aria-label="graph" :viewBox="`0 0 100 ${maxYaxis}`" preserveAspectRatio="none">
        <g :class="type">
          <path :d="valuesArea" class="graph-data-fill" />
          <path :d="valuesPath" vector-effect="non-scaling-stroke" class="graph-data-line" />
          <path :d="averageLine" vector-effect="non-scaling-stroke" class="graph-avg-line" />
        </g>
      </svg>
    </div>
    <div class="avg-tooltip" :class="type" :style="{ bottom: averageLabelPosition }">
      <div>AVG</div>
      <div role="text" aria-label="average-value" class="value">
        {{ averageLabelValue }} <span>{{ averageLabelSuffix }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "./monitoring.css";

.graph {
  position: relative;
  width: 68%;
}

.graph svg {
  position: relative;
  width: 100%;
  height: 50px;
}

.padding {
  padding: 6px 2px;
  height: 50px;
  display: flex;
  flex-direction: column;
  background-color: #f2f6f7;
}

svg {
  transform: scaleY(-1);
}

.graph .avg-tooltip {
  position: absolute;
  z-index: 10;
  right: calc(100% + 1.3em);
  display: none;
}

.graph.hover .avg-tooltip {
  display: block;
}

.graph * .graph-data-line {
  stroke-width: 1.75px;
  fill: none;
}

.graph * .graph-data-fill {
  opacity: 0.8;
}

.graph * .graph-avg-line {
  stroke-width: 1px;
  opacity: 0.5;
  stroke-dasharray: 5, 5;
}
</style>
