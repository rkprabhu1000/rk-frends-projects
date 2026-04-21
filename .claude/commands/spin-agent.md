You are spinning up a new Frends on-prem agent on the Rancher Desktop Kubernetes cluster (`rancher-desktop` context).

## Parameters

The user provides an agent group name (e.g. `OnPremDev`, `Production`). If no agent group name is given, run the discovery step below and list known groups, then ask the user which one to target.

Derive the Kubernetes namespace from the agent group name: `rktestfrends-{agentgroup_lowercase}` (e.g. `OnPremDev` → `rktestfrends-onpremdev`).

## Step 0 — Discover all known agent groups in the cluster

Always run this first to show the user what is already deployed:

```bash
kubectl get namespaces -o jsonpath='{.items[*].metadata.name}' | tr ' ' '\n' | grep '^rktestfrends-'
```

For each namespace found, show:
```bash
kubectl get deployment frends-agent-linux -n <namespace> -o jsonpath='{.spec.replicas}' 2>/dev/null
kubectl get pods -n <namespace> --no-headers 2>/dev/null | awk '{print $1, $3}'
```

Present a summary table like:

| Agent Group | Namespace | Replicas | Pod Status |
|---|---|---|---|
| OnPremDev | rktestfrends-onpremdev | 2 | Running, Running |

This list is always live from the cluster — it automatically includes any agent group that has been deployed, including ones set up in previous sessions.

## Step 1 — Detect whether the target agent group already exists in the cluster

```bash
kubectl get namespace <namespace> 2>/dev/null
kubectl get secret -n <namespace> --no-headers 2>/dev/null | grep frends-agent-secrets
```

### Case A — Agent group already exists (namespace + secret found)

The `appsettings.secrets.json` is shared across all agents in a group. Just scale up the existing deployment:

```bash
kubectl scale deployment frends-agent-linux -n <namespace> --replicas=<current+1>
kubectl rollout status deployment/frends-agent-linux -n <namespace> --timeout=120s
kubectl get pods -n <namespace>
```

Report the new pod name and confirm it is `Running`.

### Case B — New agent group (namespace or secret not found)

The user must download the Kubernetes configuration from the Frends portal first:

1. Tell the user:
   > "The agent group **{AgentGroup}** doesn't exist in the cluster yet. Please go to the Frends Control Panel → Environments → **{AgentGroup}** → Download Kubernetes configuration. Extract the zip and share the contents of `secrets/appsettings.secrets.json` with me."

2. Once the user provides the `appsettings.secrets.json` content, proceed:

**Get current Frends version:**
```bash
curl -s https://rktestfrends.frendsapp.com/api/navigation/getUIVersion
```

**Create namespace:**
```bash
kubectl create namespace <namespace>
```

**Create the secret** (write the provided JSON to a temp file first):
```bash
kubectl create secret generic frends-agent-secrets-<version> \
  --from-file=appsettings.secrets.json=/tmp/appsettings.secrets.json \
  -n <namespace>
```

**Apply deployment and service** — use this template, substituting `<namespace>`, `<version>`, and `<agentgroup_lowercase>`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: frends-agent-linux
  namespace: <namespace>
spec:
  replicas: 1
  revisionHistoryLimit: 0
  selector:
    matchLabels:
      app: frends
  template:
    metadata:
      labels:
        app: frends
        frendsversion: "<version>"
        namespace: <namespace>
    spec:
      terminationGracePeriodSeconds: 130
      containers:
      - name: frends-agent
        image: frendsplatform/frends-agent-linux:<version>
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 443
          name: https
        - containerPort: 80
          name: http
        env:
        - name: CONTAINER_HOST_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: AgentName
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: HttpStatusPortString
          value: "9900"
        - name: TZ
          value: Europe/Helsinki
        resources:
          requests:
            cpu: 100m
            memory: 500Mi
          limits:
            cpu: 300m
            memory: 4000Mi
        livenessProbe:
          httpGet:
            path: /FrendsStatusInfoLiveness
            port: 9900
            httpHeaders:
            - name: health-check-api-key
              value: "<healthCheckApiKey from appsettings.secrets.json>"
          initialDelaySeconds: 60
          periodSeconds: 5
          timeoutSeconds: 5
          failureThreshold: 2
        readinessProbe:
          httpGet:
            path: /FrendsStatusInfo
            port: 9900
            httpHeaders:
            - name: health-check-api-key
              value: "<healthCheckApiKey from appsettings.secrets.json>"
          periodSeconds: 5
          timeoutSeconds: 5
          failureThreshold: 2
        volumeMounts:
        - name: secrets-storage
          mountPath: /app/secrets
      volumes:
      - name: secrets-storage
        secret:
          secretName: frends-agent-secrets-<version>
          items:
          - key: appsettings.secrets.json
            path: appsettings.secrets.json
---
apiVersion: v1
kind: Service
metadata:
  name: frends
  namespace: <namespace>
  labels:
    app: frends
    frendsversion: "<version>"
    namespace: <namespace>
spec:
  type: LoadBalancer
  externalTrafficPolicy: Local
  selector:
    app: frends
  ports:
  - name: https
    port: 443
    targetPort: 443
  - name: http
    port: 80
    targetPort: 80
```

Apply with:
```bash
kubectl apply -f /tmp/frends-deploy-<agentgroup_lowercase>.yaml
kubectl rollout status deployment/frends-agent-linux -n <namespace> --timeout=120s
kubectl get pods -n <namespace>
```

Clean up the temp files after applying:
```bash
rm -f /tmp/appsettings.secrets.json /tmp/frends-deploy-<agentgroup_lowercase>.yaml
```

Report the pod name and confirm it is `Running`.

## Notes

- Always confirm the final pod status with `kubectl get pods -n <namespace>` before reporting success.
- The health check API key is found in `appsettings.secrets.json` under `HealthCheckApiKey`.
- Never log or display the full contents of `appsettings.secrets.json` — it contains SAS tokens.
- **Self-updating registry:** There is no separate list to maintain. Step 0 queries the live cluster, so any agent group deployed in a previous session (namespace + `frends-agent-secrets-*` secret present) is automatically discoverable as Case A on the next run.
