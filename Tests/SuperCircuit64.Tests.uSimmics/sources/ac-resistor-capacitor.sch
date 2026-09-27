<QucsStudio Schematic 5.9>
<Properties>
View=-30,-90,870,530,1,0,0
Grid=10,10,1
DataSet=*.dat
DataDisplay=*.dpl
OpenDisplay=3
showFrame=0
FrameText0=Title \n @FILE@
FrameText1=Drawn By:
FrameText2=Date: @DATE@
FrameText3=Revision:
</Properties>
<Symbol>
</Symbol>
<Components>
Vac V1 1 140 80 18 -26 0 "5 V"1"40 Hz"1"0"0"0"0"con_2"0
GND * 1 140 210 0 0 0
IProbe Pr1 1 260 80 -33 -26 3 "con_2"0
C C1 1 260 160 17 -26 1 "33 uF"1"0"0""0"neutral"0"SMD0603"0
R R1 1 200 0 -18 15 0 "180 Ω"1"26.85"0"european"0"SMD0603"0
.TR TR1 1 0 0 0 9 0 "lin"0"0"0"100ms"1"8001"1"Trapezoidal"0"1e-16"0"500"0"0.001"0"1 µA"0"yes"0"none"0
</Components>
<Wires>
140 110 140 210
140 210 260 210
260 190 260 210
140 0 140 50
140 0 170 0
230 0 260 0
260 0 260 50
260 110 260 130
260 0 260 0 "L1" 290 -50 0 ""
</Wires>
<Diagrams>
<Rect 491 209 360 220 31 #c0c0c0 1 00 1 0 0.2 1 1 -1.19841 0.5 1.20159 1 -1 0.5 1 -1 -1 -1 "" "" "">
	<Legend 10 -100 0>
	<"L1.Vt" "V" #0000ff 2 3 0 0 0 0 "">
</Rect>
<Rect 490 510 360 220 31 #c0c0c0 1 00 1 0 0.2 1 1 -0.1 0.5 1.1 1 -0.1 0.5 1.1 -1 -1 -1 "" "" "">
	<Legend 10 -100 0>
	<"Pr1.It" "A" #0000ff 2 3 0 0 0 1 "">
</Rect>
</Diagrams>
<Paintings>
</Paintings>
