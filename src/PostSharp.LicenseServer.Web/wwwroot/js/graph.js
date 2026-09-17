// Renders the licence usage history. The data is produced by GraphModel and embedded in the page as
// JSON; nothing is fetched at run time, so the page works on an isolated network.
(function () {
    "use strict";

    var dataElement = document.getElementById("usage-chart-data");
    var canvas = document.getElementById("usage-chart");

    if (!dataElement || !canvas || typeof Chart === "undefined") {
        return;
    }

    var chart = JSON.parse(dataElement.textContent);

    // Follow the page's colour scheme, so axis labels and gridlines stay legible in a dark browser.
    var styles = getComputedStyle(document.documentElement);
    Chart.defaults.color = styles.getPropertyValue("--foreground").trim() || "#222";
    Chart.defaults.borderColor = styles.getPropertyValue("--rule").trim() || "#e0e0e0";

    var datasets = [{
        label: "Used",
        data: chart.used,
        borderColor: "#58006e",
        backgroundColor: "rgba(88, 0, 110, 0.1)",
        fill: true,
        tension: 0.1,
        pointRadius: 0,
        pointHitRadius: 8
    }];

    // The capacity and grace lines are absent for an unlimited licence.
    if (chart.maximum !== null && chart.maximum !== undefined) {
        datasets.push({
            label: "Authorized",
            data: chart.labels.map(function () { return chart.maximum; }),
            borderColor: "#ffa500",
            borderDash: [6, 4],
            fill: false,
            pointRadius: 0
        });

        datasets.push({
            label: "Grace",
            data: chart.labels.map(function () { return chart.grace; }),
            borderColor: "#ff0000",
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
                    title: { display: true, text: "Concurrent users" },
                    ticks: { precision: 0 }
                },
                x: {
                    ticks: {
                        autoSkip: false,
                        maxRotation: 45,
                        minRotation: 45,
                        // The original chart labelled Mondays only, which keeps a year-long window
                        // readable.
                        callback: function (value, index) {
                            var label = chart.labels[index];
                            return new Date(label + "T00:00:00Z").getUTCDay() === 1 ? label : "";
                        }
                    }
                }
            },
            plugins: {
                legend: { position: "bottom" },
                tooltip: {
                    callbacks: {
                        title: function (items) { return items[0].label; }
                    }
                }
            }
        }
    });
})();
