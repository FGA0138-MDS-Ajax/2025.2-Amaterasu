#!/usr/bin/env bash

set -Eeuo pipefail

#
# GitHub -> Azure DevOps sync script
# Requer variáveis de ambiente:
#   AZUSERNAME      - Nome de usuário (ou e-mail) do Azure DevOps
#   AZUSER_EMAIL    - E-mail do autor do commit
#   AZORG           - Organização do Azure DevOps (ex.: myorg)
#   AZPROJECT       - Projeto do Azure DevOps (ex.: my-project)
#   AZREPO          - Nome do repositório no Azure DevOps (ex.: My Repo)
#   AZUREPAT        - Personal Access Token com permissões de Code (Read/Write)
# Opcionais:
#   AZBRANCH        - Branch de destino (padrão: branch atual)
#   COMMIT_MESSAGE  - Mensagem do commit (padrão: "Sync from GitHub to Azure DevOps")
#

# Funções auxiliares
abort() { echo "Erro: $*" >&2; exit 1; }
info() { echo "[info] $*"; }

# Normaliza e valida variáveis obrigatórias
AZUSERNAME=${AZUSERNAME:-}
AZUSER_EMAIL=${AZUSER_EMAIL:-}
AZORG=${AZORG:-}
AZPROJECT=${AZPROJECT:-}
AZREPO=${AZREPO:-}

# Suporta nomes alternativos de PAT por compatibilidade
AZUREPAT=${AZUREPAT:-${AZURE_PAT:-${AZPAT:-}}}

[[ -n "$AZUSERNAME" ]] || abort "AZUSERNAME não definido"
[[ -n "$AZUSER_EMAIL" ]] || abort "AZUSER_EMAIL não definido"
[[ -n "$AZORG" ]] || abort "AZORG não definido"
[[ -n "$AZPROJECT" ]] || abort "AZPROJECT não definido"
[[ -n "$AZREPO" ]] || abort "AZREPO não definido"
[[ -n "$AZUREPAT" ]] || abort "AZUREPAT não definido (forneça um PAT válido)"

# Codifica espaços para URL (mínimo necessário)
PROJECT_ENC=${AZPROJECT// /%20}
REPO_ENC=${AZREPO// /%20}

# URLs (sem e com credenciais, evitando vazar PAT nos logs/config)
AZ_URL="https://dev.azure.com/${AZORG}/${PROJECT_ENC}/_git/${REPO_ENC}"
CRED_URL="https://${AZUSERNAME}:${AZUREPAT}@dev.azure.com/${AZORG}/${PROJECT_ENC}/_git/${REPO_ENC}"

# Descobre branch atual se não informada
AZBRANCH=${AZBRANCH:-$(git rev-parse --abbrev-ref HEAD)}
COMMIT_MESSAGE=${COMMIT_MESSAGE:-"Sync from GitHub to Azure DevOps"}

info "Repositório Azure DevOps: ${AZ_URL}"
info "Branch de destino: ${AZBRANCH}"

# Evita remover .git; apenas garante que o repo não está raso
if [[ -f .git/shallow ]]; then
	info "Repositório raso detectado. Fazendo fetch completo..."
	git fetch --unshallow || git fetch --depth=2147483647 || true
fi

# Configura identidade de commit localmente (não global)
git config user.email "$AZUSER_EMAIL"
git config user.name "$AZUSERNAME"

# Comita apenas se houver mudanças
if ! git diff --quiet || ! git diff --cached --quiet; then
	info "Alterações detectadas. Preparando commit..."
	git add -A
	# Evita falhar se nada para commitar por corrida
	git commit -m "$COMMIT_MESSAGE" || true
else
	info "Sem alterações para commitar."
fi

# Verificação de acesso/URL do repositório no Azure DevOps
info "Verificando acesso ao repositório no Azure DevOps..."
if ! git ls-remote "$CRED_URL" &>/dev/null; then
	abort "Não foi possível acessar ${AZ_URL}. Verifique AZORG/AZPROJECT/AZREPO e permissões do PAT."
fi

# Usa um remote temporário para habilitar refs de rastreamento e evitar 'stale info' com --force-with-lease
ADO_REMOTE="ado-tmp-$$"
trap 'git remote remove "$ADO_REMOTE" 2>/dev/null || true' EXIT

# Garante que o nome temporário não exista
git remote remove "$ADO_REMOTE" 2>/dev/null || true
git remote add "$ADO_REMOTE" "$CRED_URL"

# Busca a referência do branch remoto (se existir)
info "Sincronizando com remoto (fetch + rebase)..."
if git fetch "$ADO_REMOTE" "$AZBRANCH":"refs/remotes/$ADO_REMOTE/$AZBRANCH" --prune 2>/dev/null; then
	# Rebase local no topo do remoto, se houver
	git rebase "refs/remotes/$ADO_REMOTE/$AZBRANCH" || abort "Falha no rebase contra remoto ${AZBRANCH}"
else
	info "Branch remota inexistente; prosseguindo sem rebase."
fi

# Obtém o commit esperado do branch remoto (se existir)
expected_ref=""
if expected_ref=$(git rev-parse --verify "refs/remotes/$ADO_REMOTE/$AZBRANCH" 2>/dev/null); then
	:
else
	expected_ref=""
fi

# Push com segurança (with-lease) para evitar sobrescrever alterações remotas inadvertidamente
info "Enviando alterações para Azure DevOps..."
if [[ -n "$expected_ref" ]]; then
	# Só força se o remoto ainda está no commit esperado
	git push --force-with-lease="refs/heads/$AZBRANCH:$expected_ref" "$ADO_REMOTE" "HEAD:${AZBRANCH}"
else
	# Branch remoto ainda não existe; push simples é suficiente
	git push "$ADO_REMOTE" "HEAD:${AZBRANCH}"
fi

info "Sync concluído com sucesso para ${AZ_URL} (${AZBRANCH})."