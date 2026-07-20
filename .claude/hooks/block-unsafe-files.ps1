$stdin = [Console]::In.ReadToEnd()
$inputJson = $stdin | ConvertFrom-Json
$filePath = $inputJson.tool_input.file_path

$blockedExts = @('.unity', '.meta', '.tmx', '.tsx', '.inputactions', '.asset', '.prefab')

if ($filePath) {
    $ext = [System.IO.Path]::GetExtension($filePath).ToLower()
    if ($blockedExts -contains $ext) {
        $reason = "Blocked: direct edit of '$ext' files is not allowed. Use the Unity Editor or MCP instead. (see SETUP_PLAN.md STEP2)"
        $output = @{
            hookSpecificOutput = @{
                hookEventName = "PreToolUse"
                permissionDecision = "deny"
                permissionDecisionReason = $reason
            }
        }
        $output | ConvertTo-Json -Compress
        exit 0
    }
}

exit 0
