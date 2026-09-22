# Deployment Guide

## ☸️ Kubernetes Deployment

This project uses **Kustomize** for configuration management and deploys a **multi-arch container image** (`linux/amd64` + `linux/arm64`) published to GitHub Container Registry.

### Prerequisites
1.  **Kubernetes Cluster**: Ensure your cluster is running.
2.  **NFS Server**: You need an NFS server for persistent storage.
    *   **Photos**: `/export/whiskey-photos` (For user uploads)
    *   **Backups**: `/export/whiskey-app` (Pre-migration database dumps, under `backups/`)

### 📦 Image Deployment Strategy
On every push to `main`, CI:
1.  Runs the tests.
2.  Builds a multi-arch image and pushes it to `ghcr.io/ferrinhouse/whiskeytracker.web`, tagged `sha-<full commit sha>` and `latest`. The package is public, so the cluster needs no pull secret.
3.  On the self-hosted runner, checks for pending EF migrations and, if there are any, dumps the database to `/app/backups` on the NFS backup share **before** the new version starts (the app applies migrations on startup).
4.  Deploys the new build to whichever blue/green slot is currently idle, verifies it privately, then — after manual approval — flips live traffic to it. See **🔵🟢 Blue/Green Deployments** below.

The pod has no node constraints and can run on any node, arm64 or amd64. Every node needs `nfs-common` installed for the photo volume.

### 🔵🟢 Blue/Green Deployments

There are two permanent Deployments, `whiskey-web-blue` and `whiskey-web-green`. Exactly one is "live" at a time — the `whiskey-service` Service's `spec.selector.slot` field is the single flip point that decides which one receives real traffic. The idle slot normally sits at `0` replicas.

**Check which slot is live:**
```bash
kubectl get service whiskey-service -o jsonpath='{.spec.selector.slot}'
# or, to see both replica counts:
kubectl get deploy -l app=whiskey-tracker
```

**How a deploy works now:** the `deploy-idle` job auto-detects the idle slot, deploys the new image only there, scales it up, waits for its rollout, and health-checks it privately with `kubectl exec ... wget localhost:8080/health` — **never** through the public NodePort, so the new build never receives real user traffic before it's verified. The `flip-traffic` job then runs, gated behind a **GitHub Environment approval** (see one-time setup below) — it pauses in the Actions tab until someone approves it, then patches the Service selector and scales the old slot down to 0.

**Testing the new build before approving:** once `deploy-idle` finishes, port-forward directly to the idle slot from your own machine:
```bash
kubectl port-forward deployment/whiskey-web-<idle-slot> 8081:8080
```
then browse `http://localhost:8081` — this is the real new build against the real production database, with zero public exposure, for as long as you want to test it. Approve or reject the pending deployment from the Actions tab (Actions → the running workflow → *Review deployments*) once you're satisfied. Rejecting (or just cancelling the run) means the flip never happens and production is untouched; scale the idle slot back down by hand if you don't intend to ship it soon: `kubectl scale deployment/whiskey-web-<idle-slot> --replicas=0`.

**One-time setup:** create a GitHub Environment named `production-flip` (Settings → Environments → New environment) with yourself added under **Required reviewers**. Until this exists, `flip-traffic` runs immediately with no gate.

**Rolling back:** run the **Rollback Deployment (Blue/Green)** workflow from the Actions tab (`workflow_dispatch`, type `rollback` to confirm). No rebuild, no new commit — it scales the other slot back up, health-checks it, flips the Service back, and scales down the slot you're leaving. If Actions itself is unavailable, the manual equivalent is:
```bash
kubectl scale deployment/whiskey-web-<target-slot> --replicas=1
kubectl rollout status deployment/whiskey-web-<target-slot>
kubectl patch service whiskey-service -p '{"spec":{"selector":{"slot":"<target-slot>"}}}'
kubectl scale deployment/whiskey-web-<other-slot> --replicas=0
```

> **⚠️ Migration-discipline warning:** blue/green rollback only undoes *application code* — it never undoes database migrations. `context.Database.Migrate()` runs unconditionally at container startup regardless of which slot boots, so a destructive migration (a dropped column/table, an incompatible rename) permanently forecloses rollback past that point even with a warm standby slot, because the old code's EF model may reference schema that no longer exists. `20260921163443_DropWhiskeyInStock.cs` is a real example of this kind of migration. Going forward, genuinely destructive schema changes should use an **expand/contract** pattern: ship the additive half (a new nullable column, a new table) first, let it bake through at least one full deploy-and-verify cycle, and only ship the destructive "contract" half (dropping the old column/table) in a later, separate deploy once you're confident you won't need to roll back past that boundary.

> **Note on `k8s/app-service.yaml`:** this file is *not* part of `kustomization.yaml`'s auto-applied resources. `kubectl apply` does a full-value replace on any field present in a manifest, so if the Service were re-applied on every deploy, it would silently reset `spec.selector.slot` back to `blue` every time, undoing whatever was actually live. It's applied once by hand at initial cluster bootstrap; afterwards its live selector is owned entirely by the `deploy-idle`/`flip-traffic`/rollback workflows via `kubectl patch`. The same reasoning is why `k8s/app-deployment-blue.yaml` and `-green.yaml` omit `replicas:` entirely — CI/rollback own replica count via `kubectl scale`, and an applied `replicas:` value would get reset on every deploy the same way.

### 🔐 Managing App Secrets
We use a **SecretGenerator** strategy. The CI pipeline generates a versioned secret (e.g., `whiskey-secrets-h5k2`) automatically on every deploy.

**Required Secrets (GitHub):**
Configure `SECRETS_ENV_FILE` in GitHub with the following content:
```env
# Database Credentials
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_super_secret_password
POSTGRES_DB=whiskey_prod

# Connection String
connection-string=Host=postgres-service;Database=whiskey_prod;Username=postgres;Password=your_super_secret_password

# Authentication (Google)
Authentication__Google__ClientId=your_google_client_id
Authentication__Google__ClientSecret=your_google_client_secret

# Email
EmailSettings__Host=smtp.gmail.com
EmailSettings__Port=587
EmailSettings__User=your_email@gmail.com
EmailSettings__Password=your_app_password
```

### 🌍 Networking (NodePort)
The application is exposed via a **NodePort** service on port `30080`.
Typically, you will use a reverse proxy (like Nginx Proxy Manager) to forward traffic from `whiskeytracker.ferrinhouse.org` to `NODE_IP:30080`.

### 💾 Storage Configuration
Storage is defined in `k8s/storage.yaml`.
The NFS Server IP is injected via `k8s/patches/nfs-server.yaml`.

**To change the NFS IP:**
1.  Edit `k8s/patches/nfs-server.yaml`.
2.  Update the IP for `whiskey-db-pv`, `whiskey-photos-pv`, and `whiskey-app-pv` (the backup share).

### 🚀 Manual Deployment

**First-time cluster bootstrap** (only needed once, before blue/green exists on a fresh cluster):
```bash
# Create secrets file
echo "# TODO: Populate this file with secrets as described in the 'Managing App Secrets' section" > k8s/.env

# Apply everything except the Service (Deployments, storage, postgres)
kubectl apply -k k8s/

# Bring blue up first and confirm it's healthy before anything can route to it
kubectl scale deployment/whiskey-web-blue --replicas=1
kubectl rollout status deployment/whiskey-web-blue

# Only now create the Service — see the note in k8s/app-service.yaml on why
# it's applied once by hand and never again via `kubectl apply -k`
kubectl apply -f k8s/app-service.yaml

# Green stays at 0 until the first real deploy
kubectl scale deployment/whiskey-web-green --replicas=0
```

**Manually deploying a specific build to the idle slot** (mirrors what `deploy-idle` does):
```bash
IDLE_SLOT=green   # whichever `kubectl get service whiskey-service -o jsonpath='{.spec.selector.slot}'` is NOT
kubectl apply -k k8s/
kubectl set image deployment/whiskey-web-$IDLE_SLOT whiskey-web=ghcr.io/ferrinhouse/whiskeytracker.web:sha-<commit>
kubectl scale deployment/whiskey-web-$IDLE_SLOT --replicas=1
kubectl rollout status deployment/whiskey-web-$IDLE_SLOT
# test it — see "Testing the new build before approving" above — then flip:
kubectl patch service whiskey-service -p "{\"spec\":{\"selector\":{\"slot\":\"$IDLE_SLOT\"}}}"
```
