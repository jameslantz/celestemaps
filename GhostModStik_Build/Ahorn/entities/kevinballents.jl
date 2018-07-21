module KevinballEntities

#begin kevin!!! 

MultiplayerVarCrushBlock(x::Integer, y::Integer, width::Integer=Main.Maple.defaultBlockWidth, height::Integer=Main.Maple.defaultBlockHeight, axes::String="both") = Main.Maple.Entity("multiplayerVariableSpeedCrushBlock", x=x, y=y, width=width, height=height, axes=axes)

MultiplayerTriggerSpikesUp(x::Integer, y::Integer, width::Integer=Main.Maple.defaultSpikeWidth) = Main.Maple.Entity("multiplayerTriggerSpikesUp", x=x, y=y, width=width)
MultiplayerTriggerSpikesDown(x::Integer, y::Integer, width::Integer=Main.Maple.defaultSpikeWidth) = Main.Maple.Entity("multiplayerTriggerSpikesDown", x=x, y=y, width=width)
MultiplayerTriggerSpikesLeft(x::Integer, y::Integer, height::Integer=Main.Maple.defaultSpikeHeight) = Main.Maple.Entity("multiplayerTriggerSpikesLeft", x=x, y=y, height=height)
MultiplayerTriggerSpikesRight(x::Integer, y::Integer, height::Integer=Main.Maple.defaultSpikeHeight) = Main.Maple.Entity("multiplayerTriggerSpikesRight", x=x, y=y, height=height)

KevinRefill(x::Integer, y::Integer) = Main.Maple.Entity("kevinRefill", x=x, y=y, originX=4, originY=4)

MultiplayerControlSwitch(x::Integer, y::Integer) = Main.Maple.Entity("multiplayerControlSwitch", x=x, y=y)
GhostTouchSwitch(x::Integer, y::Integer, shortTimer::Bool=false, longTimer::Bool=false, veryLongTimer::Bool=false) = Main.Maple.Entity("ghostTouchSwitch", x=x, y=y, shortTimer=shortTimer, longTimer=longTimer, veryLongTimer=veryLongTimer)
TimedSwitchGate(x1::Integer, y1::Integer, x2::Integer=x1+16, y2::Integer=y1, width::Integer=Main.Maple.defaultBlockWidth, height::Integer=Main.Maple.defaultBlockHeight, persistent::Bool=false, sprite::String="block") = Main.Maple.Entity("timedSwitchGate", x=x1, y=y1, width=width, height=height, nodes=[(x2, y2)], persistent=persistent, sprite=sprite)

function gateFinalizer(entity)
    x, y = Main.entityTranslation(entity)

    width = Int(get(entity.data, "width", 8))
    height = Int(get(entity.data, "height", 8))

    entity.data["nodes"] = [(x + width, y)]
end

placements = Dict{String, Main.EntityPlacement}(
	"[Kevinball] Kevinball (Horizontal)" => Main.EntityPlacement(
        MultiplayerVarCrushBlock,
        "rectangle",
        Dict{String, Any}(
            "axes" => "horizontal"
        )
    ),

    "[Kevinball] Refill" => Main.EntityPlacement(
        KevinRefill
    ),

    "[Kevinball] Switch Gate (Timed)" => Main.EntityPlacement(
        TimedSwitchGate,
        "rectangle",
        Dict{String, Any}(
            "sprite" => "block"
        ),
        gateFinalizer
    ),

    "[Kevinball] Touch Switch" => Main.EntityPlacement(
        GhostTouchSwitch
    ),

    "[Kevinball] Control Switch" => Main.EntityPlacement(
        MultiplayerControlSwitch
    ),

    "[Kevinball] Touch Switch (Short)" => Main.EntityPlacement(
        GhostTouchSwitch,
        "point",
        Dict{String, Any}(
            "shortTimer" => true
        )
    ),

    "[Kevinball] Touch Switch (Long)" => Main.EntityPlacement(
        GhostTouchSwitch,
        "point",
        Dict{String, Any}(
            "longTimer" => true
        )
    ),

    "[Kevinball] Touch Switch (VLong)" => Main.EntityPlacement(
        GhostTouchSwitch,
        "point",
        Dict{String, Any}(
            "veryLongTimer" => true
        )
    )
)

frameImage = Dict{String, String}(
    "none" => "objects/crushblock/block00",
    "horizontal" => "objects/crushblock/block01",
    "vertical" => "objects/crushblock/block02",
    "both" => "objects/crushblock/block03"
)

smallFace = "objects/crushblock/idle_face"
giantFace = "objects/crushblock/giant_block00"

#Begin Spikes!!!!!!!!

variants = ["default", "cliffside", "tentacles", "reflection"]
entities = Dict{String, Function}(
    "up" => Main.Maple.SpikesUp,
    "down" => Main.Maple.SpikesDown,
    "left" => Main.Maple.SpikesLeft,
    "right" => Main.Maple.SpikesRight,
)

triggerEntities = Dict{String, Function}(
    "up" => Main.Maple.TriggerSpikesUp,
    "down" => Main.Maple.TriggerSpikesDown,
    "left" => Main.Maple.TriggerSpikesLeft,
    "right" => Main.Maple.TriggerSpikesRight,
)

multiplayerTriggerEntities = Dict{String, Function}(
    "up" => MultiplayerTriggerSpikesUp,
    "down" => MultiplayerTriggerSpikesDown,
    "left" => MultiplayerTriggerSpikesLeft,
    "right" => MultiplayerTriggerSpikesRight,
)

triggerEntitiesOrig = Dict{String, Function}(
    "up" => Main.Maple.TriggerSpikesOriginalUp,
    "down" => Main.Maple.TriggerSpikesOriginalDown,
    "left" => Main.Maple.TriggerSpikesOriginalLeft,
    "right" => Main.Maple.TriggerSpikesOriginalRight,
)

for (dir, entity) in multiplayerTriggerEntities
    key = "[Kevinball] Trigger Spikes ($(titlecase(dir)))"
    placements[key] = Main.EntityPlacement(
        entity,
        "rectangle"
    )
end

directions = Dict{String, String}(

    "multiplayerTriggerSpikesUp" => "up",
    "multiplayerTriggerSpikesDown" => "down",
    "multiplayerTriggerSpikesLeft" => "left",
    "multiplayerTriggerSpikesRight" => "right"
)

offsets = Dict{String, Tuple{Integer, Integer}}(
    "up" => (4, -4),
    "down" => (4, 4),
    "left" => (-4, 4),
    "right" => (4, 4),
)

triggerOriginalOffsets = Dict{String, Tuple{Integer, Integer}}(
    "up" => (0, 5),
    "down" => (0, -4),
    "left" => (5, 0),
    "right" => (-4, 0),
)

rotations = Dict{String, Number}(
    "up" => 0,
    "right" => pi / 2,
    "down" => pi,
    "left" => pi * 3 / 2
)

rotationOffsets = Dict{String, Tuple{Number, Number}}(
    "up" => (0.5, 0.25),
    "right" => (1, 0.675),
    "down" => (1.5, 1.125),
    "left" => (0, 1.675)
)

resizeDirections = Dict{String, Tuple{Bool, Bool}}(
    "up" => (true, false),
    "down" => (true, false),
    "left" => (false, true),
    "right" => (false, true),
)

tentacleSelectionOffsets = Dict{String, Tuple{Number, Number}}(
    "up" => (0, -8),
    "down" => (0, -8),
    "left" => (-8, 0),
    "right" => (-8, 0)
)

triggerNames = ["triggerSpikesDown", "triggerSpikesLeft", "triggerSpikesRight", "triggerSpikesUp"]
triggerRotationOffsets = Dict{String, Tuple{Number, Number}}(
    "up" => (3, -1),
    "right" => (4, 3),
    "down" => (5, 5),
    "left" => (-1, 4),
)

multiplayerTriggerNames = ["multiplayerTriggerSpikesDown", "multiplayerTriggerSpikesLeft", "multiplayerTriggerSpikesRight", "multiplayerTriggerSpikesUp"]
multiplayerTriggerRotationOffsets = Dict{String, Tuple{Number, Number}}(
    "up" => (3, -1),
    "right" => (4, 3),
    "down" => (5, 5),
    "left" => (-1, 4),
)

triggerOriginalNames = ["triggerSpikesOriginalDown", "triggerSpikesOriginalLeft", "triggerSpikesOriginalRight", "triggerSpikesOriginalUp"]

kevinColor = (98, 34, 43) ./ 255

function nodeLimits(entity::Main.Maple.Entity)
    if entity.name == "switchGate"
        return true, 1, 1
    end
    if entity.name == "timedSwitchGate"
        return true, 1, 1
    end
end

function minimumSize(entity::Main.Maple.Entity)
    if entity.name == "variableSpeedCrushBlock" || entity.name == "multiplayerVariableSpeedCrushBlock"
        return true, 24, 24
    end

    if haskey(directions, entity.name)
        variant = get(entity.data, "type", "default")
        direction = get(directions, entity.name, "up")

        if variant == "tentacles"
            return true, 16, 16

        else
            return true, 8, 8
        end
    end

    if entity.name == "switchGate"
        return true, 16, 16
    end

    if entity.name == "timedSwitchGate"
        return true, 16, 16
    end
end

function resizable(entity::Main.Maple.Entity)
    if entity.name == "variableSpeedCrushBlock" || entity.name == "multiplayerVariableSpeedCrushBlock"
        return true, true, true
    end

    if haskey(directions, entity.name)
        variant = get(entity.data, "type", "default")
        direction = get(directions, entity.name, "up")

        if variant == "tentacles"
            return true, true, true

        else
            return true, resizeDirections[direction]...
        end
    end

    if entity.name == "switchGate"
        return true, true, true
    end

    if entity.name == "timedSwitchGate"
        return true, true, true
    end
end

function selection(entity::Main.Maple.Entity)
	if entity.name == "refill" || entity.name == "kevinRefill"
        x, y = Main.entityTranslation(entity)

        return true, Main.Rectangle(x - 6, y - 6, 12, 12)
    end

    if entity.name == "variableSpeedCrushBlock" || entity.name == "multiplayerVariableSpeedCrushBlock"
        x, y = Main.entityTranslation(entity)

        width = Int(get(entity.data, "width", 8))
        height = Int(get(entity.data, "height", 8))

        return true, Main.Rectangle(x, y, width, height)
    end

    if haskey(directions, entity.name)
        x, y = Main.entityTranslation(entity)

        width = Int(get(entity.data, "width", 8))
        height = Int(get(entity.data, "height", 8))

        direction = get(directions, entity.name, "up")
        variant = get(entity.data, "type", "default")

        if variant == "tentacles"
            ox, oy = tentacleSelectionOffsets[direction]

            width = Int(get(entity.data, "width", 16))
            height = Int(get(entity.data, "height", 16))

            return true, Main.Rectangle(x + ox, y + oy, width, height)

        else
            width = Int(get(entity.data, "width", 8))
            height = Int(get(entity.data, "height", 8))

            ox, oy = offsets[direction]

            return true, Main.Rectangle(x + ox - 4, y + oy - 4, width, height)
        end
    end

    if entity.name == "timedSwitchGate"
        x, y = Main.entityTranslation(entity)
        stopX, stopY = Int.(entity.data["nodes"][1])

        width = Int(get(entity.data, "width", 8))
        height = Int(get(entity.data, "height", 8))

        return true, [Main.Rectangle(x, y, width, height), Main.Rectangle(stopX, stopY, width, height)]

    elseif entity.name == "ghostTouchSwitch" || entity.name == "multiplayerControlSwitch"
        x, y = Main.entityTranslation(entity)

        return true, Main.Rectangle(x - 7, y - 7, 14, 14)
    end
end

iconSprite = Main.sprites["objects/switchgate/icon00"]

function renderGateSwitch(ctx::Main.Cairo.CairoContext, x::Number, y::Number, width::Number, height::Number, sprite::String)
    tilesWidth = div(width, 8)
    tilesHeight = div(height, 8)

    frame = "objects/switchgate/$sprite"

    for i in 2:tilesWidth - 1
        Main.drawImage(ctx, frame, x + (i - 1) * 8, y, 8, 0, 8, 8)
        Main.drawImage(ctx, frame, x + (i - 1) * 8, y + height - 8, 8, 16, 8, 8)
    end

    for i in 2:tilesHeight - 1
        Main.drawImage(ctx, frame, x, y + (i - 1) * 8, 0, 8, 8, 8)
        Main.drawImage(ctx, frame, x + width - 8, y + (i - 1) * 8, 16, 8, 8, 8)
    end

    for i in 2:tilesWidth - 1, j in 2:tilesHeight - 1
        Main.drawImage(ctx, frame, x + (i - 1) * 8, y + (j - 1) * 8, 8, 8, 8, 8)
    end

    Main.drawImage(ctx, frame, x, y, 0, 0, 8, 8)
    Main.drawImage(ctx, frame, x + width - 8, y, 16, 0, 8, 8)
    Main.drawImage(ctx, frame, x, y + height - 8, 0, 16, 8, 8)
    Main.drawImage(ctx, frame, x + width - 8, y + height - 8, 16, 16, 8, 8)

    Main.drawImage(ctx, iconSprite, x + div(width - iconSprite.width, 2), y + div(height - iconSprite.height, 2))
end

function render(ctx::Main.Cairo.CairoContext, entity::Main.Maple.Entity, room::Main.Maple.Room)
    # Todo?
    # Use tiles randomness to decide on Kevin border

    if entity.name == "ghostTouchSwitch" || entity.name == "multiplayerControlSwitch"
        Main.drawSprite(ctx, "objects/touchswitch/container.png", 0, 0)
        Main.drawSprite(ctx, "objects/touchswitch/icon00.png", 0, 0)
        
        return true
    end

    if entity.name == "kevinRefill"
        Main.drawSprite(ctx, "objects/refill/idle00.png", 0, 0)

        return true
    end

    if entity.name == "variableSpeedCrushBlock" || entity.name == "multiplayerVariableSpeedCrushBlock"
        axes = get(entity.data, "axes", "both")
        chillout = get(entity.data, "chillout", false)

        x = Int(get(entity.data, "x", 0))
        y = Int(get(entity.data, "y", 0))

        width = Int(get(entity.data, "width", 32))
        height = Int(get(entity.data, "height", 32))

        giant = height >= 48 && width >= 48 && chillout
        face = giant? giantFace : smallFace
        frame = frameImage[lowercase(axes)]
        faceSprite = Main.sprites[face]

        tilesWidth = div(width, 8)
        tilesHeight = div(height, 8)

        Main.drawRectangle(ctx, 2, 2, width - 4, height - 4, kevinColor)
        Main.drawImage(ctx, faceSprite, div(width - faceSprite.width, 2), div(height - faceSprite.height, 2))

        for i in 2:tilesWidth - 1
            Main.drawImage(ctx, frame, (i - 1) * 8, 0, 8, 0, 8, 8)
            Main.drawImage(ctx, frame, (i - 1) * 8, height - 8, 8, 24, 8, 8)
        end

        for i in 2:tilesHeight - 1
            Main.drawImage(ctx, frame, 0, (i - 1) * 8, 0, 8, 8, 8)
            Main.drawImage(ctx, frame, width - 8, (i - 1) * 8, 24, 8, 8, 8)
        end

        Main.drawImage(ctx, frame, 0, 0, 0, 0, 8, 8)
        Main.drawImage(ctx, frame, width - 8, 0, 24, 0, 8, 8)
        Main.drawImage(ctx, frame, 0, height - 8, 0, 24, 8, 8)
        Main.drawImage(ctx, frame, width - 8, height - 8, 24, 24, 8, 8)

        return true
    end

    if haskey(directions, entity.name)
        variant = entity.name in triggerNames? "trigger" : get(entity.data, "type", "default")
        direction = get(directions, entity.name, "up")
        triggerOriginalOffset = entity.name in triggerOriginalNames ? triggerOriginalOffsets[direction] : (0, 0)

        if variant == "tentacles"
            width = get(entity.data, "width", 16)
            height = get(entity.data, "height", 16)

            for ox in 0:16:width - 16, oy in 0:16:height - 16
                drawX, drawY = (ox, oy) .+ (16, 16) .* rotationOffsets[direction] .+ triggerOriginalOffset
                Main.drawSprite(ctx, "danger/tentacles00.png", drawX, drawY, rot=rotations[direction])
            end

            if width / 8 % 2 == 1 || height / 8 % 2 == 1
                drawX, drawY = (width - 16, height - 16) .+ (16, 16) .* rotationOffsets[direction] .+ triggerOriginalOffset
                Main.drawSprite(ctx, "danger/tentacles00.png", drawX, drawY, rot=rotations[direction])
            end

        elseif variant == "trigger"
            width = get(entity.data, "width", 8)
            height = get(entity.data, "height", 8)

            updown = direction == "up" || direction == "down"

            for ox in 0:8:width - 8, oy in 0:8:height - 8
                drawX, drawY = (ox, oy) .+ triggerRotationOffsets[direction] .+ triggerOriginalOffset
                Main.drawSprite(ctx, "danger/triggertentacle/wiggle_v06.png", drawX, drawY, rot=rotations[direction])
                Main.drawSprite(ctx, "danger/triggertentacle/wiggle_v03.png", drawX + 3 * updown, drawY + 3 * !updown, rot=rotations[direction])
            end

        else        
            width = get(entity.data, "width", 8)
            height = get(entity.data, "height", 8)

            for ox in 0:8:width - 8, oy in 0:8:height - 8
                drawX, drawY = (ox, oy) .+ offsets[direction] .+ triggerOriginalOffset
                Main.drawSprite(ctx, "danger/spikes/$(variant)_$(direction)00.png", drawX, drawY)
            end
        end

        return true
    end

    return false
end

function renderSelectedAbs(ctx::Main.Cairo.CairoContext, entity::Main.Maple.Entity)
    if haskey(directions, entity.name)
        direction = get(directions, entity.name, "up")
        theta = rotations[direction] - pi / 2

        width = Int(get(entity.data, "width", 0))
        height = Int(get(entity.data, "height", 0))

        x, y = Main.entityTranslation(entity)
        cx, cy = x + floor(Int, width / 2) - 8 * (direction == "left"), y + floor(Int, height / 2) - 8 * (direction == "up")

        Main.drawArrow(ctx, cx, cy, cx + cos(theta) * 24, cy + sin(theta) * 24, Main.colors.selection_selected_fc, headLength=6)
    end

    if entity.name == "switchGate" || entity.name == "timedSwitchGate"
        sprite = get(entity.data, "sprite", "block")
        startX, startY = Int(entity.data["x"]), Int(entity.data["y"])
        stopX, stopY = Int.(entity.data["nodes"][1])

        width = Int(get(entity.data, "width", 32))
        height = Int(get(entity.data, "height", 32))

        renderGateSwitch(ctx, stopX, stopY, width, height, sprite)
        Main.drawArrow(ctx, startX + width / 2, startY + height / 2, stopX + width / 2, stopY + height / 2, Main.colors.selection_selected_fc, headLength=6)

        return true
    end
end

function renderAbs(ctx::Main.Cairo.CairoContext, entity::Main.Maple.Entity, room::Main.Maple.Room)
    if entity.name == "timedSwitchGate"
        sprite = get(entity.data, "sprite", "block")

        x = Int(get(entity.data, "x", 0))
        y = Int(get(entity.data, "y", 0))

        width = Int(get(entity.data, "width", 32))
        height = Int(get(entity.data, "height", 32))

        renderGateSwitch(ctx, x, y, width, height, sprite)

        return true
    end

    return false
end

end