// Draws the usage history of a license. GraphModel produces the data, and the page contains it as
// JSON. The page downloads nothing at run time, so it works on a network without access to the
// Internet.
(function () {
    "use strict";

    var dataElement = document.getElementById("usage-chart-data");
    var canvas = document.getElementById("usage-chart");

    if (!dataElement || !canvas || typeof Chart === "undefined") {
        return;
    }

    var chart = JSON.parse(dataElement.textContent);

    // The colors of the axes and of the grid lines come from the design tokens, so that the chart
    // belongs to the page instead of appearing as a white rectangle on it.
    var styles = getComputedStyle(document.documentElement);
    Chart.defaults.color = styles.getPropertyValue("--text-muted").trim() || "#a0a0a0";
    Chart.defaults.borderColor = styles.getPropertyValue("--doc-table-line").trim() || "#2c1a4f";
    Chart.defaults.font.family = styles.getPropertyValue("--font-body").trim() || "sans-serif";

    var datasets = [{
        label: "Seats",
        data: chart.seats,
        borderColor: "#973bfc",
        backgroundColor: "rgba(151, 59, 252, 0.16)",
        fill: true,
        tension: 0.1,
        pointRadius: 0,
        pointHitRadius: 8
    }];

    // A license without a seat limit has no capacity line and no grace line.
    if (chart.maximum !== null && chart.maximum !== undefined) {
        datasets.push({
            label: "Authorized",
            data: chart.labels.map(function () { return chart.maximum; }),
            borderColor: "#38d5e4",
            borderDash: [6, 4],
            fill: false,
            pointRadius: 0
        });

        datasets.push({
            label: "Grace",
            data: chart.labels.map(function () { return chart.grace; }),
            borderColor: "#ff5e45",
            borderDash: [2, 3],
            fill: false,
            pointRadius: 0
        });
    }

    new Chart(canvas, {
        type: "line",
        data: { labels: chart.labels, datasets: datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: "index", intersect: false },
            scales: {
                y: {
                    beginAtZero: true,
                    max: chart.axisMaximum,
                    title: { display: true, text: "Seats" },
                    ticks: { precision: 0 }
                },
                x: {
                    ticks: {
                        autoSkip: false,
                        maxRotation: 45,
                        minRotation: 45,
                        // The original chart labelled only the Mondays, which keeps a window of one
                        // year readable.
                        callback: function (value, index) {
                            var label = chart.labels[index];
                            return new Date(label + "T00:00:00Z").getUTCDay() === 1 ? label : "";
                        }
                    }
                }
            },
            plugins: {
                legend: {
                    position: "bottom",

                    // Each series is a line, so the legend draws a line and not the filled rectangle
                    // that Chart.js draws by default. The dash pattern and the width are copied from
                    // the dataset, because the generated legend item does not contain them. Without
                    // them, the three samples of the legend would differ only by their color, while
                    // the lines of the chart are solid, dashed and dotted.
                    labels: {
                        usePointStyle: true,
                        pointStyle: "line",
                        boxWidth: 32,
                        generateLabels: function (instance) {
                            var items = Chart.defaults.plugins.legend.labels.generateLabels(instance);

                            items.forEach(function (item) {
                                var dataset = instance.data.datasets[item.datasetIndex];
                                item.lineDash = dataset.borderDash || [];
                                item.lineWidth = dataset.borderWidth || 2;
                            });

                            return items;
                        }
                    }
                },
                tooltip: {
                    callbacks: {
                        title: function (items) { return items[0].label; }
                    }
                }
            }
        }
    });
})();
