# Story 018 frame times: X Damage updates counted for 60 s on one pinned view

Each run: the Linux player at 1280x720 on Xvfb with Mesa's llvmpipe software renderer (4 CPU cores), the Feel Lab's
gravity at 0.001 and 'Apply: restart at top', so every run and every build draws the test slope's start view, where
the snow mesh covers the near snow and the track map is stamped; `--screenshot`/`--after` keep the player drawing in the
background. A frame is two updates (the player's window and the window manager's frame around it). Runs alternate
builds within a batch; the two batches ran hours apart on a machine whose speed changed between them (the same build
before the story drew 1.39 s a frame in the first and 2.38 s in the second), so only numbers within a batch compare.

Software rendering puts every triangle through the CPU, so these numbers say how much more work the mesh is, not what a
GPU makes of it: 60 frames a second on the user's PC is the user's check.

| Batch (UTC, 2026-11-02) | Build | What it draws | Seconds a frame |
|---|---|---|---|
| 1 (02:44 to 02:51) | c557d50 (run 41372681433) | before the story | 1.37, 1.41 |
| 1 | 29c86a5 (run 41375908154) | the snow mesh, its add pass always on | 4.30, 4.01 |
| 2 (16:31 to 16:42) | 0d62120 (run 41378281068) | the snow mesh, its add pass always on | 6.15, 5.81 |
| 2 | 49c1146 (run 41477452195) | the snow mesh, its add pass only where slush lies (none near the start) | 5.24, 5.34 |
| 2 | c557d50 | before the story | 2.38 |

So in software rendering the mesh costs about 3 times the frame (batch 1) and, with the add pass left out where it
adds nothing, about 2.2 times (batch 2); leaving the add pass out saves about 11 percent of the frame. Each run counted
8 to 42 frames, so a run's number is good to about a tenth.

## The counts (each run's first line waits for the window and is left out of the averages)

### time/before1

```text
0.000 5 1357.2
1.499 2 1353.0
2.852 2 1364.1
4.216 2 1342.2
5.558 2 1332.3
6.891 2 1352.1
8.243 2 1318.5
9.561 2 1400.4
10.962 2 1323.0
12.285 2 1382.9
13.668 2 1298.3
14.966 2 1347.4
16.314 2 1331.7
17.645 2 1361.8
19.007 2 1355.6
20.363 2 1386.5
21.749 2 1335.8
23.085 2 1338.0
24.423 2 1409.0
25.832 2 1417.4
27.250 2 1382.5
28.632 2 1352.4
29.985 2 1451.0
31.436 2 1419.7
32.855 2 1482.4
34.338 2 1462.7
35.801 2 1443.2
37.244 2 1381.2
38.625 2 1361.5
39.987 2 1364.5
41.351 2 1323.8
42.675 2 1331.4
44.006 2 1363.5
45.370 2 1384.9
46.755 2 1305.6
48.060 2 1359.7
49.420 2 1358.7
50.779 2 1402.0
52.181 2 1330.1
53.511 2 1312.5
54.824 2 1412.9
56.236 2 1303.6
57.540 2 1289.3
```

### time/after1

```text
0.000 5 4274.9
5.130 2 4300.6
9.431 2 4179.5
13.610 2 4225.0
17.835 2 4193.3
22.029 2 4210.9
26.240 2 4412.3
30.652 2 4338.4
34.990 2 4237.7
39.228 2 4239.1
43.467 2 4112.0
47.579 2 4054.8
51.634 2 4413.4
```

### time/before2

```text
0.000 5 1414.7
2.404 2 1333.6
3.737 2 1407.5
5.145 2 1425.8
6.571 2 1407.6
7.978 2 1364.7
9.343 2 1363.3
10.706 2 1584.8
12.291 2 1371.9
13.663 2 1363.0
15.026 2 1339.6
16.366 2 1357.0
17.723 2 1508.3
19.231 2 1444.2
20.675 2 1335.8
22.011 2 1289.6
23.301 2 1307.7
24.608 2 1389.1
25.997 2 1253.2
27.251 2 1314.2
28.565 2 1294.2
29.859 2 1363.3
31.222 2 1342.8
32.565 2 1418.0
33.983 2 1434.2
35.418 2 1370.8
36.788 2 1421.1
38.210 2 1487.8
39.697 2 1369.4
41.067 2 1393.8
42.461 2 1341.5
43.802 2 1367.8
45.170 2 1348.7
46.519 2 1328.8
47.848 2 1320.6
49.168 2 1389.4
50.558 2 1400.2
51.958 2 1318.4
53.276 2 1567.5
54.844 2 1463.1
56.307 2 1388.6
57.696 2 1538.1
```

### time/after2

```text
0.000 3 1070.4
1.070 2 4318.1
5.389 2 4287.7
9.677 2 4299.9
13.977 2 4456.8
18.434 2 4228.6
22.662 2 4183.4
26.846 2 4228.9
31.075 2 4089.4
35.164 2 4223.0
39.387 2 4301.1
43.688 2 4294.6
47.983 2 4161.9
52.145 2 4234.4
```

### time2/noskip1

```text
0.000 3 3220.3
3.220 2 6504.7
9.725 2 6677.9
16.403 2 7194.7
23.598 2 6684.9
30.283 2 6395.4
36.678 2 6116.2
42.794 2 6420.7
49.215 2 6145.8
```

### time2/skip1

```text
0.000 3 2140.9
2.141 2 5832.2
7.973 2 5941.0
13.914 2 5603.7
19.518 2 5419.8
24.938 2 5473.9
30.412 2 5406.2
35.818 2 5369.8
41.188 2 5514.8
46.703 2 5679.5
52.382 2 5407.0
```

### time2/noskip2

```text
0.000 3 2075.7
2.076 2 6121.1
8.197 2 6339.0
14.536 2 6430.0
20.966 2 6527.9
27.494 2 6343.9
33.838 2 6177.7
40.016 2 6044.2
46.060 2 6212.1
52.272 2 6384.8
```

### time2/skip2

```text
0.000 3 2290.6
2.291 2 5867.3
8.158 2 5692.0
13.850 2 5656.3
19.506 2 5340.7
24.847 2 5715.0
30.562 2 5805.8
36.368 2 5569.4
41.937 2 5949.2
47.887 2 5539.4
53.426 2 5747.0
```

### time2/before1

```text
0.000 5 2210.2
3.046 2 2316.5
5.362 2 2331.0
7.693 2 2267.6
9.961 2 2157.8
12.119 2 2224.6
14.343 2 2360.3
16.704 2 2410.5
19.114 2 2456.2
21.570 2 2337.9
23.908 2 2269.7
26.178 2 2225.7
28.404 2 2341.1
30.745 2 2293.8
33.039 2 2327.1
35.366 2 2469.1
37.835 2 2392.1
40.227 2 2447.8
42.675 2 2192.5
44.868 2 2292.1
47.160 2 2311.4
49.471 2 2388.1
51.859 2 2549.7
54.409 2 2649.8
57.059 2 2525.9
```
