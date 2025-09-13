name: Push github directory to azure devops repository
on:
  push:
    branches:
      - '*' # applies for all branches in github.
  workflow_dispatch: # Manual trigger from GitHub UI
  
jobs:
  check-bats-version:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
      with:
        fetch-depth: 0 # necessário para unshallow e with-lease funcionarem corretamente
    - name: Run script file
      env:
        AZUREPAT: ${{secrets.AZPAT}} # calling secrets from environment variables
        AZUSERNAME: ${{secrets.AZUSERNAME}}
        AZUSER_EMAIL: ${{secrets.AZUSER_EMAIL}}
        AZORG: ${{secrets.AZORG}}
        AZPROJECT: ${{secrets.AZPROJECT}}
        AZREPO: ${{secrets.AZREPO}}
        AZBRANCH: ${{ github.ref_name }}
      run: |
         chmod +x ./script/commit.sh
         ./script/commit.sh # it will calls 'commit.sh' file. Thats where our logic is in.
      shell: bash