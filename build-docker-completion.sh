#!/usr/bin/env bash
# ============================
# Shell completion for build-docker.sh
#
# Supports: bash and zsh
#
# INSTALL (one-time, pick the one that matches your shell):
#
#   bash — add to ~/.bashrc or ~/.bash_profile:
#     source /path/to/build-docker-completion.sh
#
#   zsh  — add to ~/.zshrc:
#     source /path/to/build-docker-completion.sh
#
#   Or run once per session:
#     source ./build-docker-completion.sh
# ============================

# Known services — keep in sync with all_services in build-docker.sh
_build_docker_services=(
  "App_Gateway"
  "App_Auth"
  "App_Storage"
  "App_Contract"
)

# ------------------------------------------------------------------
# bash completion
# ------------------------------------------------------------------
_build_docker_bash() {
  local cur prev words
  _get_comp_words_by_ref -n : cur prev words 2>/dev/null || {
    cur="${COMP_WORDS[COMP_CWORD]}"
    prev="${COMP_WORDS[COMP_CWORD-1]}"
  }

  local all_flags="--registry --service --platform --deploy --no-pull --no-push --only-push --production --help"

  case "$prev" in
    --service)
      # Suggest known service names
      COMPREPLY=( $(compgen -W "${_build_docker_services[*]}" -- "$cur") )
      return 0
      ;;
    --platform)
      COMPREPLY=( $(compgen -W "all amd64 arm64" -- "$cur") )
      return 0
      ;;
    --registry)
      # No fixed completions — let the user type
      COMPREPLY=()
      return 0
      ;;
    --deploy)
      # Complete to yaml/yml files
      COMPREPLY=( $(compgen -f -X "!*.y?(a)ml" -- "$cur") )
      return 0
      ;;
  esac

  # Default: complete flags
  COMPREPLY=( $(compgen -W "$all_flags" -- "$cur") )
}

# ------------------------------------------------------------------
# zsh completion
# ------------------------------------------------------------------
_build_docker_zsh() {
  local services_joined="${_build_docker_services[*]}"

  _arguments \
    '--registry[Registry URL to push images to]:registry URL:' \
    "--service[Services to build (comma-separated or *)]:service:($services_joined)" \
    '--platform[Target platform]:platform:(all amd64 arm64)' \
    '--deploy[Path to docker-compose file (optional)]:compose file:_files -g "*.y(a)ml"' \
    '--no-pull[Skip git pull]' \
    '--no-push[Build locally, skip push]' \
    '--only-push[Skip build, push existing images only]' \
    '--production[Use production image tags]' \
    '(- *)--help[Show help]'
}

# ------------------------------------------------------------------
# Registration
# ------------------------------------------------------------------
if [[ -n "$ZSH_VERSION" ]]; then
  # zsh: enable completion system if not already enabled
  if ! command -v compdef >/dev/null 2>&1; then
    autoload -Uz compinit && compinit
  fi
  compdef _build_docker_zsh ./build-docker.sh build-docker.sh
else
  # bash
  complete -F _build_docker_bash ./build-docker.sh build-docker.sh
fi
