<script setup>
import { onMounted, onUnmounted, ref } from 'vue'

const apiBase = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'

const activeTab = ref('manual')
const manualCode = ref(`var query = "SELECT * FROM Users WHERE Name = '" + input + "'";`)
const manualLoading = ref(false)
const manualError = ref('')
const manualResult = ref(null)

const commits = ref([])
const selectedCommitId = ref(null)
let pollTimer = null

async function analyzeCode() {
  manualLoading.value = true
  manualError.value = ''

  try {
    const response = await fetch(`${apiBase}/api/analyze`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        rawCode: manualCode.value,
        language: 'csharp'
      })
    })

    if (!response.ok) {
      throw new Error('Analyze request failed.')
    }

    manualResult.value = await response.json()
  } catch (error) {
    manualError.value = error.message || 'Unexpected error while analyzing code.'
  } finally {
    manualLoading.value = false
  }
}

async function fetchCommitResults() {
  try {
    const response = await fetch(`${apiBase}/api/webhook/results`)
    if (!response.ok) {
      throw new Error('Failed to load webhook scan results.')
    }

    commits.value = await response.json()
  } catch (error) {
    console.error(error)
  }
}

function selectCommit(commitId) {
  selectedCommitId.value = selectedCommitId.value === commitId ? null : commitId
}

onMounted(() => {
  fetchCommitResults()
  pollTimer = setInterval(fetchCommitResults, 3000)
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
        <h1 class="text-3xl font-semibold text-cyan-300">AI-powered Code Vulnerability Intelligence</h1>
        <p class="mt-2 text-sm text-slate-300">
          Detect risky commits and auto-generate secure C# remediation patterns.
        </p>
      </header>

      <section class="rounded-2xl border border-slate-800 bg-slate-900/70 shadow-xl">
        <div class="flex gap-2 border-b border-slate-800 p-3">
          <button
            class="rounded-xl px-4 py-2 text-sm font-medium transition"
            :class="activeTab === 'manual' ? 'bg-cyan-500 text-slate-950' : 'bg-slate-800 text-slate-200 hover:bg-slate-700'"
            @click="activeTab = 'manual'"
          >
            Manual Code Scan
          </button>
          <button
            class="rounded-xl px-4 py-2 text-sm font-medium transition"
            :class="activeTab === 'monitor' ? 'bg-cyan-500 text-slate-950' : 'bg-slate-800 text-slate-200 hover:bg-slate-700'"
            @click="activeTab = 'monitor'"
          >
            GitHub Commit Monitor
          </button>
        </div>

        <div v-if="activeTab === 'manual'" class="space-y-4 p-5">
          <textarea
            v-model="manualCode"
            rows="8"
            class="w-full rounded-xl border border-slate-700 bg-slate-950 p-4 font-mono text-sm text-slate-100 outline-none focus:border-cyan-500"
          />

          <button
            class="rounded-xl bg-cyan-500 px-5 py-2 font-semibold text-slate-950 transition hover:bg-cyan-400 disabled:cursor-not-allowed disabled:opacity-60"
            :disabled="manualLoading"
            @click="analyzeCode"
          >
            {{ manualLoading ? 'Analyzing…' : 'Analyze & Auto-Fix' }}
          </button>

          <p v-if="manualError" class="text-sm text-rose-400">{{ manualError }}</p>

          <div v-if="manualResult" class="space-y-4">
            <div class="grid gap-4 md:grid-cols-3">
              <article class="rounded-xl border border-slate-800 bg-slate-950 p-4">
                <h3 class="text-xs uppercase text-slate-400">Type</h3>
                <p class="mt-1 text-lg font-semibold text-cyan-300">{{ manualResult.vulnerabilityType }}</p>
              </article>
              <article class="rounded-xl border border-slate-800 bg-slate-950 p-4">
                <h3 class="text-xs uppercase text-slate-400">Severity</h3>
                <p class="mt-1 text-lg font-semibold text-amber-300">{{ manualResult.severity }}</p>
              </article>
              <article class="rounded-xl border border-slate-800 bg-slate-950 p-4">
                <h3 class="text-xs uppercase text-slate-400">Verdict</h3>
                <p class="mt-1 text-lg font-semibold" :class="manualResult.isVulnerable ? 'text-rose-300' : 'text-emerald-300'">
                  {{ manualResult.isVulnerable ? 'Vulnerable' : 'Clean' }}
                </p>
              </article>
            </div>

            <article class="rounded-xl border border-slate-800 bg-slate-950 p-4">
              <h3 class="text-xs uppercase text-slate-400">Explanation</h3>
              <p class="mt-2 whitespace-pre-wrap text-sm text-slate-200">{{ manualResult.explanation }}</p>
            </article>

            <div class="grid gap-4 lg:grid-cols-2">
              <article class="rounded-xl border border-rose-900/60 bg-slate-950 p-4">
                <h3 class="text-xs uppercase text-rose-300">Original Code</h3>
                <pre class="mt-2 overflow-x-auto whitespace-pre-wrap text-sm text-slate-200">{{ manualResult.originalCode }}</pre>
              </article>
              <article class="rounded-xl border border-emerald-900/60 bg-slate-950 p-4">
                <h3 class="text-xs uppercase text-emerald-300">AI Fixed Code</h3>
                <pre class="mt-2 overflow-x-auto whitespace-pre-wrap text-sm text-slate-200">{{ manualResult.suggestedFix }}</pre>
              </article>
            </div>
          </div>
        </div>

        <div v-else class="p-5">
          <div v-if="commits.length === 0" class="rounded-xl border border-dashed border-slate-700 bg-slate-950 p-6 text-sm text-slate-300">
            Waiting for webhook events… post to <code>/api/webhook/github</code> and results will appear here.
          </div>

          <div v-else class="space-y-3">
            <article
              v-for="commit in commits"
              :key="commit.id"
              class="overflow-hidden rounded-xl border border-slate-800 bg-slate-950"
            >
              <button
                class="flex w-full items-start justify-between gap-4 p-4 text-left"
                @click="selectCommit(commit.id)"
              >
                <div>
                  <p class="font-semibold text-cyan-300">{{ commit.commitId || 'Unknown commit' }}</p>
                  <p class="mt-1 text-sm text-slate-300">{{ commit.author || 'Unknown author' }} — {{ commit.message }}</p>
                </div>
                <span class="rounded-lg px-2 py-1 text-xs font-semibold"
                  :class="commit.isVulnerable ? 'bg-rose-900/60 text-rose-200' : 'bg-emerald-900/60 text-emerald-200'">
                  {{ commit.severity }}
                </span>
              </button>

              <div v-if="selectedCommitId === commit.id" class="space-y-3 border-t border-slate-800 p-4 text-sm text-slate-200">
                <p><span class="font-semibold text-slate-400">Type:</span> {{ commit.vulnerabilityType }}</p>
                <p class="whitespace-pre-wrap"><span class="font-semibold text-slate-400">Explanation:</span> {{ commit.explanation }}</p>
                <div class="grid gap-3 lg:grid-cols-2">
                  <div class="rounded-lg border border-slate-800 bg-slate-900 p-3">
                    <p class="text-xs uppercase text-slate-400">Original Code</p>
                    <pre class="mt-2 overflow-x-auto whitespace-pre-wrap">{{ commit.originalCode }}</pre>
                  </div>
                  <div class="rounded-lg border border-slate-800 bg-slate-900 p-3">
                    <p class="text-xs uppercase text-slate-400">Security Patch</p>
                    <pre class="mt-2 overflow-x-auto whitespace-pre-wrap">{{ commit.suggestedFix }}</pre>
                  </div>
                </div>
              </div>
            </article>
          </div>
        </div>
      </section>
    </div>
  </main>
</template>
