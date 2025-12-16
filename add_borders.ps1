# Script to add Border wrappers to all controller StackPanel items
$filePath = "d:\PBL4\PBL4-test\WPFUI_NEW\Views\KeyMappingView.xaml"
$content = Get-Content $filePath -Raw

# Fix literal \n in Left Trigger
$content = $content -replace '`n', "`n"

# List of all controller items to wrap (excluding Left Bumper which already has Border)
$items = @(
    'Left Stick Up',
    'Left Stick Right', 
    'Left Stick Down',
    'Left Stick Left',
    'D-Pad Up',
    'D-Pad Right',
    'D-Pad Down',
    'D-Pad Left',
    'Right Bumper',
    'Right Trigger',
    'Y Button',
    'B Button', 
    'A Button',
    'X Button',
    'Right Stick Up',
    'Right Stick Right',
    'Right Stick Down',
    'Right Stick Left',
    'Menu',
    'View',
    'Home'
)

foreach ($item in $items) {
    $pattern = "(\s+<!-- $item -->\s+)<StackPanel Orientation=`"Horizontal`" (Canvas\.Left=`"\d+`" Canvas\.Top=`"\d+`" Panel\.ZIndex=`"\d+`")>([\s\S]+?)</StackPanel>"
    $replacement = "`$1<Border Background=`"White`" CornerRadius=`"8`" Padding=`"10,8`" `$2>`n                    <Border.Effect>`n                        <DropShadowEffect Color=`"#667eea`" BlurRadius=`"8`" ShadowDepth=`"2`" Opacity=`"0.15`"/>`n                    </Border.Effect>`n                    <StackPanel Orientation=`"Horizontal`">`$3</StackPanel>`n                </Border>"
    $content = $content -replace $pattern, $replacement
}

$content | Set-Content $filePath -NoNewline
Write-Host "Done! Added Border wrappers to all controller items."
