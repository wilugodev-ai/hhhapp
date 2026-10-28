param([ValidatePattern('^(hhh-[a-z0-9-]+|ablest-20261023)$')][string]$ProjectId='ablest-20261023', [string]$Region='us-central1')
$ErrorActionPreference='Stop'
$gcloud=if($env:HHH_GCLOUD){$env:HHH_GCLOUD}else{'gcloud.cmd'}
$private=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) '.artifacts/cloud/private'
New-Item -ItemType Directory -Force -Path $private | Out-Null
$acl=Get-Acl -LiteralPath $private
$acl.SetAccessRuleProtection($true,$false)
$identity=[Security.Principal.WindowsIdentity]::GetCurrent().User
$rule=[Security.AccessControl.FileSystemAccessRule]::new($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
$acl.SetAccessRule($rule)
Set-Acl -LiteralPath $private -AclObject $acl
function Cloud([string[]]$Arguments) { & $gcloud @Arguments; if ($LASTEXITCODE) { throw "Google $($Arguments[0]) failed." } }
$existing = Cloud @('projects','list',"--filter=projectId=$ProjectId",'--format=value(projectId)')
if (!$existing) {
  if ($ProjectId -notmatch '^hhh-') { throw 'Expected the existing hosting project.' }
  Cloud @('projects','create',$ProjectId,'--name=Healthcare Horizon Hub','--quiet')
}
$enabled = Cloud @('billing','projects','describe',$ProjectId,'--format=value(billingEnabled)')
if ($enabled -ne 'True') { throw 'Use a hosting project with billing already enabled; new-project billing quota is exhausted.' }
Cloud @('services','enable','run.googleapis.com','cloudbuild.googleapis.com','artifactregistry.googleapis.com','secretmanager.googleapis.com','storage.googleapis.com',"--project=$ProjectId",'--quiet')
foreach ($name in @('hhh-runtime','hhh-build')) {
  $account = Cloud @('iam','service-accounts','list',"--project=$ProjectId","--filter=email:$name@$ProjectId.iam.gserviceaccount.com",'--format=value(email)')
  if (!$account) { Cloud @('iam','service-accounts','create',$name,"--project=$ProjectId",'--quiet') }
}
$builder="hhh-build@$ProjectId.iam.gserviceaccount.com"
Cloud @('projects','add-iam-policy-binding',$ProjectId,"--member=serviceAccount:$builder",'--role=roles/logging.logWriter','--condition=None','--quiet','--format=none')
$repository=Cloud @('artifacts','repositories','list',"--project=$ProjectId","--location=$Region",'--filter=name:/hhh','--format=value(name)')
if (!$repository) { Cloud @('artifacts','repositories','create','hhh','--repository-format=docker',"--project=$ProjectId","--location=$Region",'--quiet') }
Cloud @('artifacts','repositories','add-iam-policy-binding','hhh',"--project=$ProjectId","--location=$Region","--member=serviceAccount:$builder",'--role=roles/artifactregistry.writer','--condition=None','--quiet','--format=none')
$bucket="$ProjectId-hhh-source"
$buckets=@(Cloud @('storage','buckets','list',"--project=$ProjectId",'--format=value(name)'))
if ($buckets -notcontains $bucket -and $buckets -notcontains "gs://$bucket") { Cloud @('storage','buckets','create',"gs://$bucket","--project=$ProjectId","--location=$Region",'--uniform-bucket-level-access','--public-access-prevention','--quiet') }
Cloud @('storage','buckets','add-iam-policy-binding',"gs://$bucket","--member=serviceAccount:$builder",'--role=roles/storage.objectViewer','--quiet','--format=none')
Write-Host "HHH cloud project, build identity, repository and private source bucket prepared: $ProjectId."
