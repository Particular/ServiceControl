<script setup lang="ts">
import { ref, computed } from "vue";
import { useFormatTime, useFormatLargeNumber } from "../../composables/formatter";
import { useGraph } from "./graphLines";
import type { PlotData } from "./PlotData";

const props = defineProps<{
  isdurationgraph: boolean;
  metricsuffix?: string | undefined;
  firstdataseries: PlotData;
  seconddataseries?: PlotData | undefined;
  minimumyaxis?: number | undefined;
  avgdecimals: number;
  firstseriestype?: string | undefined;
  secondseriestype?: string | undefined;
}>();

const hover = ref(false);

const minPoints = computed(() => Math.max(props.firstdataseries.points.length, props.seconddataseries?.points?.length ?? 0, 10));
const series1 = useGraph(
  () => props.firstdataseries,
  () => props.minimumyaxis,
  () => minPoints.value
);
const series2 = useGraph(
  () => props.seconddataseries,
  () => props.minimumyaxis,
  () => minPoints.value
);
const maxYaxis = computed(() => padToWholeValue(Math.max(series1.maxYaxis.value, series2?.maxYaxis.value ?? 0)));
const tickValues = computed(() => {
  const ticks = [0, (maxYaxis.value * 1) / 4, (maxYaxis.value * 1) / 2, (maxYaxis.value * 3) / 4, maxYaxis.value];
  const durationTick = (tick: number) => {
    const formattedTime = useFormatTime(tick);
    return `${formattedTime.value} ${formattedTime.unit}`;
  };
  return props.isdurationgraph ? ticks.map((tick) => durationTick(tick)) : ticks;
});

function padToWholeValue(value: number) {
  const emptyDataSetyAxisMax = 10;

  if (!value) {
    return emptyDataSetyAxisMax;
  }

  let upperBound = 10;

  while (value > upperBound) {
    upperBound *= 10;
  }

  upperBound /= 10;

  return Math.floor(value / upperBound) * upperBound + upperBound;
}

const series1AverageLabelValue = computed(() => (props.isdurationgraph ? useFormatTime(series1.average.value).value : useFormatLargeNumber(series1.average.value, 2)));
const series2AverageLabelValue = computed(() => (props.isdurationgraph ? useFormatTime(series2.average.value).value : useFormatLargeNumber(series2.average.value, 2)));
const series1AverageLabelSuffix = computed(() => (props.isdurationgraph ? useFormatTime(series1.average.value).unit.toUpperCase() : (props.metricsuffix ?? "")));
const series2AverageLabelSuffix = computed(() => (props.isdurationgraph ? useFormatTime(series2.average.value).unit.toUpperCase() : (props.metricsuffix ?? "")));
//NOTE: using hard coded height of graph (200px - 10 for padding). To get it exact without hard coding a height value, we would need to perform measurement on the rendered SVG element, which we want to avoid
const series1AverageLabelPosition = computed(() => `calc(${(series1.average.value / maxYaxis.value) * 190}px - 1em)`);
const series2AverageLabelPosition = computed(() => `calc(${(series2.average.value / maxYaxis.value) * 190}px - 1em)`);
</script>

<template>
  <div class="graph large-graph pull-left" :class="{ hover: hover }" @mouseover="hover = true" @mouseout="hover = false">
    <div class="padding">
      <svg class="y-axis">
        <!-- 
          Offsets explained:
            - 60px inset from the left for the y-axis values to show
            - 5px padding above and below to prevent the bottom and top y-axis value from being cut off by the edge of the SVG 
              (font size 10px for class 'tick', offset half a character = 5px)
            - transform 60px from the left and 5px down for the same reasons as above
        -->
        <rect height="calc(100% - 10px)" transform="translate(60, 5)" fill="#F2F6F7" width="100%"></rect>
        <g height="calc(100% - 10px)" transform="translate(60, 5)" fill="none" font-size="10" font-family="sans-serif" text-anchor="end">
          <g class="tick" opacity="1" v-for="(tickValue, i) in tickValues" :key="tickValue" :transform="`translate(0,${(4 - i) * (190 / 4)})`">
            <rect height="1.75px" fill="black" width="100%" opacity="0.1"></rect>
            <text fill="#828282" x="-4" dy="0.32em">{{ tickValue }}</text>
          </g>
        </g>
      </svg>
      <svg class="data" :viewBox="`0 0 100 ${maxYaxis}`" preserveAspectRatio="none">
        <g :class="props.firstseriestype">
          <path :d="series1.valuesArea.value" class="graph-data-fill" />
          <path :d="series1.valuesPath.value" vector-effect="non-scaling-stroke" class="graph-data-line" />
          <path :d="series1.averageLine.value" vector-effect="non-scaling-stroke" class="graph-avg-line" />
        </g>
        <g :class="props.secondseriestype">
          <path :d="series2.valuesArea.value" class="graph-data-fill" />
          <path :d="series2.valuesPath.value" vector-effect="non-scaling-stroke" class="graph-data-line" />
          <path :d="series2.averageLine.value" vector-effect="non-scaling-stroke" class="graph-avg-line" />
        </g>
      </svg>
    </div>
    <div class="avg-tooltip" :class="firstseriestype" :style="{ bottom: series1AverageLabelPosition }">
      <div>AVG</div>
      <div class="value">
        {{ series1AverageLabelValue }} <span>{{ series1AverageLabelSuffix }}</span>
      </div>
    </div>
    <div v-if="props.seconddataseries" class="avg-tooltip left" :class="secondseriestype" :style="{ bottom: series2AverageLabelPosition }">
      <div>AVG</div>
      <div class="value">
        {{ series2AverageLabelValue }} <span>{{ series2AverageLabelSuffix }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "./monitoring.css";

.large-graph {
  position: relative;
  width: 100%;
}

.large-graph svg {
  position: absolute;
  width: 100%;
  height: 100%;
  top: 0;
  left: 0;
}

svg.data {
  transform: scaleY(-1);
  left: 60px;
  top: 5px;
  /* see padding offsets explained in the template. Needs to be px sizing to align with SVG paths */
  width: calc(100% - 60px);
  height: calc(100% - 10px);
}

.large-graph .graph-data-line {
  stroke-width: 2.75;
  fill: none;
}

.large-graph * .graph-data-fill {
  opacity: 0.8;
}

.large-graph .graph-avg-line {
  stroke-width: 1.5;
  opacity: 0.5;
  stroke-dasharray: 10, 10;
}

.padding {
  height: 200px;
  display: flex;
  flex-direction: column;
  position: relative;
}

.large-graph .avg-tooltip {
  position: absolute;
  z-index: 10;
  right: calc(100% - 60px + 1.3em);
  width: fit-content;
  display: none;
}

.large-graph .avg-tooltip.left {
  left: calc(100% + 1.3em);
}

.large-graph.hover .avg-tooltip {
  display: block;
}
</style>
