# SFS-Closest-Approach-Line
Brings back the beloved closest approach line from SFS old versions

When you select a target, this mod will display the closest approach line that shows the position on the orbit your ship will be the closest to the target.
You can use this to modify your trajectory so that you get an encounter with the desired object.

The closest approach line is displayed in light blue to be easily distinguished from other informations. When the closest approach is getting close to 0, the line gradually turns red to warn you about a collision risk.

This is a less powerful tool than the embedded navigation system, but when this one fails, the closest approach line will still be there for you.


# New feature since V2.3: closest approach on multi-turns

When the player orbit intersects with the targeted orbit, the game also shows 2 green lines (one for each of the crossing points) that show the best approach on several turns. They work in a similar way as the original blue line, but there are a few differences:

- The line doesn't show the best approach strictly speaking, but the "approach at node", which is the approach calculated when the player object is at the crossing point.
- Nothing will be shown if the orbits don't intersect. This is intended.
- The green line always shows an approach over at least 2 turns. Once there's less than 1 turn remaining, the green line is recalculated at a later date, the more precise blue line is to be used to handle the final approach.
- The best approach is calculated over at most 20 turns. The calculation of the best approach also tries to minimize the number of turns to avoid excessive time-warping. In short, it won't make you time-warp over many extra turns for just a slightly better result. Your ΔV matters, but your time matters more at some point
