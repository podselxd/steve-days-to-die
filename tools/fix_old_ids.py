"""One-off: the first sheets had 7 ids written from memory that are not in the game's data.
Replaced with the real ids found by preflight (2026-11-11)."""
import io, os
from gamedata import ROOT

FIX = {
    "blocks": [("woodFrameBlock", "frameShapes")],
    "effects": [("buffHealOverTime", "buffHealHealth"), ("buffStrength", "buffDrugSteroids"),
                ("buffPoison", "buffInfectionMain"), ("buffBurning", "buffBurningElement")],
    "mobs": [("animalPig", "no_match")],
    "vehicles": [("vehicleGyroCopterPlaceable", "vehicleGyrocopterPlaceable")],
}
for f, rs in FIX.items():
    p = os.path.join(ROOT, "sheets", f + ".json")
    s = io.open(p, encoding="utf-8").read()
    for a, b in rs:
        s = s.replace('"%s"' % a, '"%s"' % b)
    io.open(p, "w", encoding="utf-8").write(s)
print("ok")
