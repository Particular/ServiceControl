<script setup lang="ts"></script>

<template>
  <div class="box">
    <div class="row">
      <div class="col">
        <div class="card">
          <div class="card-header">Filters</div>
          <div class="card-body">
            <h5 class="card-title">Special title treatment</h5>
            <div class="card-text">
              <div>
                <label>Search by name: <input type="text" placeholder="endpoint name" /></label>
              </div>
              <div class="form-floating">
                <select class="form-select" id="floatingSelect" aria-label="Floating label select example">
                  <option value="1" selected>All</option>
                  <option value="2">Retrieved from Broker</option>
                  <option value="3">Known to ServiceControl</option>
                </select>
                <label for="floatingSelect">Display</label>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div class="col">Sort</div>
    </div>
  </div>

  <div class="box">
    <div class="row">
      <div class="col">Endpoint (Queue)</div>
      <div class="col">Max daily throughput this month</div>
      <div class="col">Is Send only?</div>
      <div class="col">Do not include?</div>
    </div>
    <div class="row">
      <div class="col">My.Endpoint</div>
      <div class="col">1234</div>
      <div class="col">
        <div class="form-check"><input class="form-check-input" type="checkbox" value="" /><label class="form-check-label" for="">Send only</label></div>
      </div>
      <div class="col">
        <div class="form-check"><input class="form-check-input" type="checkbox" value="" /><label class="form-check-label" for="">Do not include</label></div>
      </div>
    </div>
  </div>
</template>

<style scoped></style>
