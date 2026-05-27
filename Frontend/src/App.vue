<script setup>
import { onMounted, onUnmounted, reactive, ref } from 'vue'

const apiBase = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'

const activeTab = ref('scanner')

const manualCode = ref(`api_key = "super-secret-key"`)
const manualResult = ref(null)
const manualLoading = ref(false)
const manualError = ref('')

const manualHistory = ref([])
const webhookHistory = ref([])
let pollTimer = null

const rules = ref([])
const ruleLoading = ref(false)
const ruleError = ref('')
const ruleForm = reactive({
  vulnerabilityType: '',
  regexPattern: '',
  severity: 'Medium',
  explanation: '',
  suggestedFix: ''
})

const pentestLoading = ref(false)
const pentestResult = ref(null)
const pentestError = ref('')

async function analyzeCode() {
  manualLoading.value = true
  manualError.value = ''

  try {
    const response = await fetch(`${apiBase}/api/analyze`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ rawCode: manualCode.value, language: 'generic' })
    })

    if (!response.ok) {
      throw new Error('Analyze request failed.')
    }

    manualResult.value = await response.json()
    await fetchManualHistory()
  } catch (error) {
    manualError.value = error.message || 'Unexpected analyze error.'
  } finally {
    manualLoading.value = false
  }
}

async function fetchManualHistory() {
  try {
    const response = await fetch(`${apiBase}/api/analyze/history`)
    if (!response.ok) {
      throw new Error('Failed to load manual scan history.')
    }

    manualHistory.value = await response.json()
  } catch (error) {
    console.error(error)
  }
}

async function fetchWebhookHistory() {
  try {
    const response = await fetch(`${apiBase}/api/webhook/results`)
    if (!response.ok) {
      throw new Error('Failed to load webhook history.')
    }

    webhookHistory.value = await response.json()
  } catch (error) {
    console.error(error)
  }
}

async function fetchRules() {
  ruleLoading.value = true
  ruleError.value = ''
  try {
    const response = await fetch(`${apiBase}/api/dataset/all`)
    if (!response.ok) {
      throw new Error('Failed to load rules.')
    }

    rules.value = await response.json()
  } catch (error) {
    ruleError.value = error.message || 'Failed to load rules.'
  } finally {
    ruleLoading.value = false
  }
}

async function addRule() {
  ruleError.value = ''
  try {
    const response = await fetch(`${apiBase}/api/dataset/add`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(ruleForm)
    })

    if (!response.ok) {
      const payload = await response.json().catch(() => null)
      throw new Error(payload?.message || 'Failed to add rule.')
    }

    ruleForm.vulnerabilityType = ''
    ruleForm.regexPattern = ''
    ruleForm.severity = 'Medium'
    ruleForm.explanation = ''
    ruleForm.suggestedFix = ''

    await fetchRules()
  } catch (error) {
    ruleError.value = error.message || 'Failed to add rule.'
  }
}

async function runPentest() {
  pentestLoading.value = true
  pentestError.value = ''
  pentestResult.value = null

  try {
    const response = await fetch(`${apiBase}/api/pentest/run`, {
      method: 'POST'
    })

    if (!response.ok) {
      throw new Error('Pentest execution failed.')
    }

    pentestResult.value = await response.json()
  } catch (error) {
    pentestError.value = error.message || 'Failed to run pentest.'
  } finally {
    pentestLoading.value = false
  }
}

onMounted(async () => {
  await Promise.all([fetchManualHistory(), fetchWebhookHistory(), fetchRules()])
  pollTimer = setInterval(() => {
    fetchManualHistory()
    fetchWebhookHistory()
  }, 3000)
})

onUnmounted(() => {
  if (pollTimer) {
    clearInterval(pollTimer)
  }
})
</script>

<template>
  <main class="min-h-screen bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 p-6 md:p-10">
    <div class="mx-auto max-w-7xl space-y-6">
      <header class="rounded-2xl border border-slate-800 bg-slate-900/70 p-6 shadow-xl">
        <h1 class="text-3xl font-semibold text-cyan-300">Local Vulnerability Intelligence</h1>
        <p class="mt-2 text-sm text-slate-300">MongoDB-backed AST/Regex scanner with local pentest simulation.</p>
      </header>

      <section class="rounded-2xl border border-slate-800 bg-slate-900/70 shadow-xl">
        <div class="flex flex-wrap gap-2 border-b border-slate-800 p-3">
          <button
            class="rounded-xl px-4 py-2 text-sm font-medium transition"
            :class="activeTab === 'scanner' ? 'bg-cyan-500 text-slate-950' : 'bg-slate-800 text-slate-200 hover:bg-slate-700'"
            @click="activeTab = 'scanner'"
          >
            Scanner Dashboard
          </button>
          <button
            class="rounded-xl px-4 py-2 text-sm font-medium transition"
            :class="activeTab === 'knowledge' ? 'bg-cyan-500 text-slate-950' : 'bg-slate-800 text-slate-200 hover:bg-slate-700'"
            @click="activeTab = 'knowledge'"
          >
            Knowledge Base Management
          </button>
          <button
            class="rounded-xl px-4 py-2 text-sm font-medium transition"
            :class="activeTab === 'pentest' ? 'bg-cyan-500 text-slate-950' : 'bg-slate-800 text-slate-200 hover:bg-slate-700'"
            @click="activeTab = 'pentest'"
          >
            Pentest Runner
          </button>
        </div>

        <div v-if="activeTab === 'scanner'" class="space-y-6 p-5">
          <div class="rounded-xl border border-slate-800 bg-slate-950 p-4">
            <h2 class="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">Manual Scan</h2>
            <textarea
              v-model="manualCode"
              rows="8"
              class="w-full rounded-xl border border-slate-700 bg-slate-950 p-4 font-mono text-sm text-slate-100 outline-none focus:border-cyan-500"
            />
            <button
              class="mt-3 rounded-xl bg-cyan-500 px-5 py-2 font-semibold text-slate-950 transition hover:bg-cyan-400 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="manualLoading"
              @click="analyzeCode"
            >
              {{ manualLoading ? 'Scanning...' : 'Run Scan' }}
            </button>
            <p v-if="manualError" class="mt-2 text-sm text-rose-400">{{ manualError }}</p>

            <div v-if="manualResult" class="mt-4 grid gap-3 md:grid-cols-3">
              <article class="rounded-lg border border-slate-800 bg-slate-900 p-3">
                <p class="text-xs uppercase text-slate-400">Type</p>
                <p class="mt-1 font-semibold text-cyan-300">{{ manualResult.vulnerabilityType }}</p>
              </article>
              <article class="rounded-lg border border-slate-800 bg-slate-900 p-3">
                <p class="text-xs uppercase text-slate-400">Severity</p>
                <p class="mt-1 font-semibold text-amber-300">{{ manualResult.severity }}</p>
              </article>
              <article class="rounded-lg border border-slate-800 bg-slate-900 p-3">
                <p class="text-xs uppercase text-slate-400">Verdict</p>
                <p class="mt-1 font-semibold" :class="manualResult.isVulnerable ? 'text-rose-300' : 'text-emerald-300'">
                  {{ manualResult.isVulnerable ? 'Vulnerable' : 'Clean' }}
                </p>
              </article>
            </div>
          </div>

          <div class="grid gap-4 lg:grid-cols-2">
            <article class="rounded-xl border border-slate-800 bg-slate-950 p-4">
              <h2 class="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">Live Manual Scans</h2>
              <div v-if="manualHistory.length === 0" class="text-sm text-slate-400">No manual scans yet.</div>
              <div v-else class="space-y-2 text-sm">
                <div v-for="entry in manualHistory" :key="entry.id" class="rounded-lg border border-slate-800 bg-slate-900 p-3">
                  <p class="font-semibold text-cyan-300">{{ entry.vulnerabilityType }} · {{ entry.severity }}</p>
                  <p class="text-slate-300">{{ entry.explanation }}</p>
                </div>
              </div>
            </article>

            <article class="rounded-xl border border-slate-800 bg-slate-950 p-4">
              <h2 class="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">Realtime Webhook Commits</h2>
              <div v-if="webhookHistory.length === 0" class="text-sm text-slate-400">No webhook events yet.</div>
              <div v-else class="space-y-2 text-sm">
                <div v-for="entry in webhookHistory" :key="entry.id" class="rounded-lg border border-slate-800 bg-slate-900 p-3">
                  <p class="font-semibold text-cyan-300">{{ entry.commitId || 'Unknown commit' }}</p>
                  <p class="text-slate-300">{{ entry.author || 'Unknown' }} — {{ entry.message }}</p>
                  <p class="text-xs" :class="entry.isVulnerable ? 'text-rose-300' : 'text-emerald-300'">
                    {{ entry.vulnerabilityType }} ({{ entry.severity }})
                  </p>
                </div>
              </div>
            </article>
          </div>
        </div>

        <div v-else-if="activeTab === 'knowledge'" class="space-y-5 p-5">
          <div class="rounded-xl border border-slate-800 bg-slate-950 p-4">
            <h2 class="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-400">Add Rule</h2>
            <form class="grid gap-3 md:grid-cols-2" @submit.prevent="addRule">
              <input v-model="ruleForm.vulnerabilityType" required placeholder="Vulnerability Type"
                class="rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100" />
              <select v-model="ruleForm.severity" class="rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100">
                <option>High</option>
                <option>Medium</option>
                <option>Low</option>
              </select>
              <input v-model="ruleForm.regexPattern" required placeholder="Regex Pattern"
                class="md:col-span-2 rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-sm text-slate-100" />
              <textarea v-model="ruleForm.explanation" rows="2" placeholder="Explanation"
                class="rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100" />
              <textarea v-model="ruleForm.suggestedFix" rows="2" placeholder="Suggested Fix"
                class="rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100" />
              <button type="submit" class="md:col-span-2 rounded-xl bg-cyan-500 px-5 py-2 font-semibold text-slate-950 hover:bg-cyan-400">
                Save Rule to MongoDB
              </button>
            </form>
            <p v-if="ruleError" class="mt-2 text-sm text-rose-400">{{ ruleError }}</p>
          </div>

          <div class="rounded-xl border border-slate-800 bg-slate-950 p-4">
            <h2 class="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">Knowledge Base Rules</h2>
            <p v-if="ruleLoading" class="text-sm text-slate-400">Loading rules...</p>
            <div v-else class="overflow-x-auto">
              <table class="min-w-full text-left text-sm text-slate-200">
                <thead>
                  <tr class="border-b border-slate-800 text-xs uppercase text-slate-400">
                    <th class="px-3 py-2">Type</th>
                    <th class="px-3 py-2">Severity</th>
                    <th class="px-3 py-2">Regex</th>
                    <th class="px-3 py-2">Explanation</th>
                    <th class="px-3 py-2">Suggested Fix</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="rule in rules" :key="rule.id" class="border-b border-slate-900 align-top">
                    <td class="px-3 py-2 font-semibold text-cyan-300">{{ rule.vulnerabilityType }}</td>
                    <td class="px-3 py-2">{{ rule.severity }}</td>
                    <td class="px-3 py-2 font-mono text-xs">{{ rule.regexPattern }}</td>
                    <td class="px-3 py-2">{{ rule.explanation }}</td>
                    <td class="px-3 py-2">{{ rule.suggestedFix }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div v-else class="space-y-4 p-5">
          <article class="rounded-xl border border-slate-800 bg-slate-950 p-4">
            <h2 class="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">Local Mock Attack</h2>
            <button
              class="rounded-xl bg-cyan-500 px-5 py-2 font-semibold text-slate-950 hover:bg-cyan-400 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="pentestLoading"
              @click="runPentest"
            >
              {{ pentestLoading ? 'Running...' : 'Run Pentest Simulation' }}
            </button>
            <p v-if="pentestError" class="mt-2 text-sm text-rose-400">{{ pentestError }}</p>
          </article>

          <article v-if="pentestResult" class="rounded-xl border border-slate-800 bg-slate-950 p-4">
            <p class="text-sm">
              Status:
              <span :class="pentestResult.status === 'Success' ? 'text-emerald-300' : 'text-rose-300'" class="font-semibold">
                {{ pentestResult.status }}
              </span>
            </p>
            <div class="mt-3 rounded-lg border border-slate-800 bg-black p-3 font-mono text-xs text-slate-200">
              <p
                v-for="(line, idx) in pentestResult.logs"
                :key="idx"
                :class="line.includes('[SUCCESS]') ? 'text-emerald-300' : line.includes('[INFO]') ? 'text-cyan-300' : 'text-rose-300'"
              >
                {{ line }}
              </p>
            </div>
          </article>
        </div>
      </section>
    </div>
  </main>
</template>
