param (
    [string]$image_tag_suffix = (Get-Date -format "yyMMdd_HHmmss")
)
$image_tag = "itoktsnhc/stat_itok_worker:$image_tag_suffix"
$commit_hash = git rev-parse HEAD
Write-Output "image from COMMIT_HASH:$commit_hash; with tag:$image_tag"
docker buildx build -f "./Dockerfile" --force-rm -t $image_tag --label "COMMIT_HASH=$commit_hash" ../
docker push $image_tag
return $image_tag