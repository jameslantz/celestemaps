module KevinballTriggers

KevinballEndzone(x::Integer, y::Integer, width::Integer=Main.Maple.defaultTriggerWidth, height::Integer=Main.Maple.defaultTriggerHeight) = Main.Maple.Trigger("kevinballEndzoneTrigger", x=x, y=y, width=width, height=height)
KevinballP1Spawn(x::Integer, y::Integer, width::Integer=Main.Maple.defaultTriggerWidth, height::Integer=Main.Maple.defaultTriggerHeight) = Main.Maple.Trigger("kevinballP1SpawnTrigger", x=x, y=y, width=width, height=height)
KevinballP2Spawn(x::Integer, y::Integer, width::Integer=Main.Maple.defaultTriggerWidth, height::Integer=Main.Maple.defaultTriggerHeight) = Main.Maple.Trigger("kevinballP2SpawnTrigger", x=x, y=y, width=width, height=height)

placements = Dict{String, Main.EntityPlacement}(
    "[Kevinball] Endzone Trigger" => Main.EntityPlacement(
        KevinballEndzone,
        "rectangle"
    ),

    "[Kevinball] P1 Spawn" => Main.EntityPlacement(
        KevinballP1Spawn,
        "rectangle"
    ),

    "[Kevinball] P2 Spawn" => Main.EntityPlacement(
        KevinballP2Spawn,
        "rectangle"
    ),
)

end