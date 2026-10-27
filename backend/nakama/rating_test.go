package main

import "testing"

func TestElo(t *testing.T) {
	for _, c := range []struct{ a, b, winner, wantA, wantB int }{
		{1000, 1000, 0, 1016, 984}, {1000, 1000, 1, 984, 1016},
		{1000, 1000, 2, 1000, 1000}, {1200, 800, 0, 1203, 797},
		{1200, 800, 1, 1171, 829}, {1200, 800, 2, 1187, 813},
		{0, 0, 0, 16, 0}, {0, 1000, 1, 0, 1000},
	} {
		a, b := eloAfter(c.a, c.b, c.winner)
		if a != c.wantA || b != c.wantB {
			t.Errorf("Elo(%d,%d,%d)=(%d,%d), want (%d,%d)", c.a, c.b, c.winner, a, b, c.wantA, c.wantB)
		}
		if a < 0 || b < 0 {
			t.Error("Elo must preserve the lower bound")
		}
	}
}

func TestSearchWindow(t *testing.T) {
	for _, c := range []struct {
		age  float64
		want int
	}{{-1, 100}, {0, 100}, {19.999, 100}, {20, 200}, {39.999, 200}, {40, 300}, {60, 400}, {10000, 400}} {
		if got := searchRadius(c.age); got != c.want {
			t.Errorf("radius(%v)=%d, want %d", c.age, got, c.want)
		}
	}
	if !ratingPairAllowed(1000, 1100, 100, 100) {
		t.Error("inclusive initial boundary rejected")
	}
	if ratingPairAllowed(1000, 1180, 200, 100) {
		t.Error("one-sided widening must not force a fresh opponent")
	}
	if !ratingPairAllowed(1000, 1180, 200, 200) {
		t.Error("mutually expanded window rejected")
	}
	if ratingPairAllowed(1000, 1401, 400, 400) {
		t.Error("maximum skill gap exceeded")
	}
}
