<script setup lang="ts">
import SignalRChat from './components/SignalRChat.vue'
</script>

<template>
  <SignalRChat />
</template>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  background: #0f172a;
  color: #e2e8f0;
  min-height: 100vh;
}

#app {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
}
</style>
