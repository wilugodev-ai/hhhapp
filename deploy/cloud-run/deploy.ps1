param([string]$ProjectId='ablest-20261023',[string]$Region='us-central1',[switch]$BuildOnly,[string]$ExistingImage)
$ErrorActionPreference='Stop'
if ($ProjectId -notmatch '^(hhh-[a-z0-9-]+|ablest-20261023)$') { throw 'Unexpected hosting project.' }
$gcloud=if($env:HHH_GCLOUD){$env:HHH_GCLOUD}else{'gcloud.cmd'}
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work=Join-Path $repoRoot '.artifacts/cloud'
New-Item -ItemType Directory -Force -Path $work | Out-Null
function Cloud([string[]]$Arguments) { & $gcloud @Arguments; if ($LASTEXITCODE) { throw "Google $($Arguments[0]) failed." } }
function JsonFile([string]$Name,$Value) { $file=Join-Path $work $Name; [IO.File]::WriteAllText($file,($Value|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false)); return $file }
Push-Location $repoRoot
try {
  $files=@(Cloud @('meta','list-files-for-upload'))
  $allowed='^(\.gcloudignore|\.dockerignore|global\.json|Directory\.Build\.props|deploy/cloud-run/Dockerfile|src/Hhh\.(Api|Domain|Infrastructure)/.+\.(cs|csproj|json)|apps/web/(package(-lock)?\.json|angular\.json|tsconfig[^/]*\.json|src/.+|public/.+))$'
  foreach ($file in $files) {
    $normal=([string]$file).Replace('\','/') -replace '^\./',''
    if ($normal -notmatch $allowed -or $normal -match '(^|/)(\.env[^/]*|\.artifacts|node_modules|obj|bin|\.git)(/|$)' -or $normal -match '\.(key|pem|pfx|log)$|appsettings\..*Local\.json$') { throw "Unexpected upload file: $normal" }
  }
  if (!$files.Count) { throw 'No source files selected.' }
  $image=if($ExistingImage){$ExistingImage}else{"$Region-docker.pkg.dev/$ProjectId/hhh/app:$(Get-Date -Format 'yyyyMMdd-HHmmss')"}
  if ($image -notmatch ('^'+[regex]::Escape("$Region-docker.pkg.dev/$ProjectId/hhh/app:")+'[a-zA-Z0-9_.-]+$')) { throw 'Unexpected HHH image.' }
  if (!$ExistingImage) {
    $configuration=JsonFile 'build.json' @{
      steps=@(@{name='gcr.io/cloud-builders/docker';args=@('build','--file','deploy/cloud-run/Dockerfile','--target','runtime','--tag',$image,'.')});
      images=@($image);serviceAccount="projects/$ProjectId/serviceAccounts/hhh-build@$ProjectId.iam.gserviceaccount.com";options=@{logging='CLOUD_LOGGING_ONLY'};timeout='1200s'
    }
    $id=(Cloud @('builds','submit','.',"--project=$ProjectId","--region=$Region","--config=$configuration","--gcs-source-staging-dir=gs://$ProjectId-hhh-source/source",'--async','--format=value(id)','--quiet')).Trim()
    JsonFile 'build-result.json' @{projectId=$ProjectId;region=$Region;buildId=$id;image=$image;status='submitted'} | Out-Null
    Write-Host "HHH build submitted: $id. Check .artifacts/cloud/build-result.json."
    return
  }
  if ($BuildOnly) { return }
  $projectNumber=(Cloud @('projects','describe',$ProjectId,'--format=value(projectNumber)')).Trim()
  $hosts=@('localhost','127.0.0.1',"hhh-$projectNumber.$Region.run.app")
  $existingUrl=(Cloud @('run','services','list',"--project=$ProjectId","--region=$Region",'--filter=metadata.name=hhh','--format=value(status.url)'))
  if ($existingUrl) { $hosts+=([uri]([string]$existingUrl).Trim()).Host }
  $environment=JsonFile 'environment.json' @{
    ASPNETCORE_ENVIRONMENT='Production';Database__Provider='PostgreSql';Database__MigrateOnStartup='false';Demo__Enabled='false';SampleData__Enabled='true';
    DataProtection__CertificatePath='/secrets/hhh/session-encryption.pfx';AllowedHosts=($hosts -join ';');Logging__LogLevel__Default='Warning';'Logging__LogLevel__Microsoft.Hosting.Lifetime'='Information'
  }
  Cloud @('run','deploy','hhh',"--project=$ProjectId","--region=$Region","--image=$image","--service-account=hhh-runtime@$ProjectId.iam.gserviceaccount.com",'--allow-unauthenticated','--ingress=all','--port=8080','--cpu=1','--memory=512Mi','--cpu-throttling','--min=0','--max=1','--min-instances=0','--max-instances=1','--concurrency=8','--timeout=120',"--env-vars-file=$environment",'--set-secrets=ConnectionStrings__Hhh=hhh-database-connection:1,/secrets/hhh/session-encryption.pfx=hhh-data-protection-cert:1','--quiet')
  $url=(Cloud @('run','services','describe','hhh',"--project=$ProjectId","--region=$Region",'--format=value(status.url)')).Trim()
  if (([uri]$url).Host -notin $hosts) {
    $hosts+=([uri]$url).Host
    Cloud @('run','services','update','hhh',"--project=$ProjectId","--region=$Region",('--update-env-vars=AllowedHosts='+($hosts -join ';')),'--quiet')
  }
  JsonFile 'deployment-result.json' @{projectId=$ProjectId;region=$Region;url=$url;image=$image;database='hhh';runtimeRole='hhh_app_20261027';localData='preserved'} | Out-Null
  Write-Host "HHH deployed: $url"
} finally { Pop-Location }
